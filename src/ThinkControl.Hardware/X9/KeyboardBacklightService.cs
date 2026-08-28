using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ThinkControl.Hardware.X9;

public enum KeyboardBacklightLevel
{
    Off = 0,
    Low = 1,
    High = 2,
    FirmwareAuto = 3
}

/// <summary>
/// Capability-probed Lenovo keyboard-backlight access.
/// Direct Lenovo PM-driver contracts are preferred. If those do not expose a
/// recognized state, ThinkControl can reuse the installed Lenovo Vantage
/// ThinkKeyboard add-in on verified ThinkPad hardware and still requires readback.
/// </summary>
public sealed class KeyboardBacklightService : IDisposable
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;

    // These contracts intentionally match the public Lenovo keyboard-backlight
    // controller implementations that use IBMPmDrv/EnergyDrv. Do not add guessed
    // X9 IOCTLs here: unknown writes remain disabled until a read contract proves
    // the backend is compatible.
    private static readonly DriverConfig[] Drivers =
    [
        new(
            "Lenovo PM Driver · ThinkPad",
            @"\\.\IBMPmDrv",
            0x00222680,
            null,
            0x00050200,
            0x00050201,
            0x00050202,
            null,
            0x00222684,
            0x00000000,
            0x00000001,
            0x00000002),

        new(
            "Lenovo EnergyDrv · standard",
            @"\\.\EnergyDrv",
            0x83102144,
            0x00000032,
            0x00000001,
            0x00000003,
            0x00000005,
            null,
            0x83102144,
            0x00000033,
            0x00010033,
            0x00020033),

        new(
            "Lenovo EnergyDrv · alternate",
            @"\\.\EnergyDrv",
            0x83102144,
            0x00000032,
            0x00010001,
            0x00010003,
            0x00010005,
            0x00010007,
            0x83102144,
            0x00000033,
            0x00010033,
            0x00020033)
    ];

    private static readonly string[] VantageKeyboardRoots =
    [
        @"C:\ProgramData\Lenovo\Vantage\Addins\ThinkKeyboardAddin",
        @"C:\ProgramData\Lenovo\VantageService\Addins\ThinkKeyboardAddin",
        @"C:\ProgramData\Lenovo\ImController\Plugins\ThinkKeyboardPlugin"
    ];

    private SafeFileHandle? _handle;
    private DriverConfig? _driver;
    private VantageKeyboardBackend? _vantage;
    private DateTimeOffset _lastVantageProbe = DateTimeOffset.MinValue;
    private string? _lastBackendLabel;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint ioControlCode,
        byte[]? inBuffer,
        int inBufferSize,
        byte[]? outBuffer,
        int outBufferSize,
        out int bytesReturned,
        IntPtr overlapped);

    public string BackendLabel => _lastBackendLabel ?? _driver?.Name ?? _vantage?.Label ?? "Not exposed";

    public bool IsAvailable
    {
        get
        {
            EnsureOpen();
            if (_handle is { IsInvalid: false, IsClosed: false } && _driver is not null &&
                TryGet(_driver, _handle, out _))
            {
                _lastBackendLabel = _driver.Name;
                return true;
            }

            if (EnsureVantageBackend() && _vantage?.TryGet(out _) == true)
            {
                _lastBackendLabel = _vantage.Label;
                return true;
            }
            return false;
        }
    }

    public bool TryGet(out KeyboardBacklightLevel level)
    {
        level = KeyboardBacklightLevel.Off;
        EnsureOpen();

        if (_handle is { IsInvalid: false, IsClosed: false } && _driver is not null &&
            TryGet(_driver, _handle, out level))
        {
            _lastBackendLabel = _driver.Name;
            return true;
        }

        if (EnsureVantageBackend() && _vantage?.TryGet(out level) == true)
        {
            _lastBackendLabel = _vantage.Label;
            return true;
        }
        return false;
    }

    public bool SetAndVerify(KeyboardBacklightLevel level)
    {
        EnsureOpen();

        // FirmwareAuto=3 is an observed Lenovo/Vantage contract and is verified by
        // readback. Direct-driver configs intentionally have no guessed SetAuto
        // payload, so OEM Auto is attempted only through the installed Lenovo API.
        if (level == KeyboardBacklightLevel.FirmwareAuto)
        {
            if (!EnsureVantageBackend() || _vantage?.SetAndVerify(level) != true)
                return false;
            _lastBackendLabel = _vantage.Label;
            return true;
        }

        if (_handle is { IsInvalid: false, IsClosed: false } && _driver is not null)
        {
            uint payload = level switch
            {
                KeyboardBacklightLevel.Off => _driver.SetOff,
                KeyboardBacklightLevel.Low => _driver.SetLow,
                KeyboardBacklightLevel.High => _driver.SetHigh,
                _ => _driver.SetOff
            };

            byte[] input = BitConverter.GetBytes(payload);
            var output = new byte[16];
            if (!DeviceIoControl(
                    _handle,
                    _driver.SetIoctl,
                    input,
                    input.Length,
                    output,
                    output.Length,
                    out _,
                    IntPtr.Zero))
            {
                return false;
            }

            // Lenovo firmware/readback can lag the successful IOCTL by more than one
            // scheduler quantum. Verify a few bounded times instead of declaring the
            // working provider dead after one 55 ms sample.
            for (int attempt = 0; attempt < 4; attempt++)
            {
                Thread.Sleep(attempt == 0 ? 55 : 70);
                if (TryGet(out KeyboardBacklightLevel current) && current == level)
                {
                    _lastBackendLabel = _driver.Name;
                    return true;
                }
            }
            return false;
        }

        if (EnsureVantageBackend() && _vantage?.SetAndVerify(level) == true)
        {
            _lastBackendLabel = _vantage.Label;
            return true;
        }
        return false;
    }

    private void EnsureOpen()
    {
        if (_handle is { IsInvalid: false, IsClosed: false } && _driver is not null)
            return;

        _handle?.Dispose();
        _handle = null;
        _driver = null;

        foreach (DriverConfig candidate in Drivers)
        {
            SafeFileHandle handle = CreateFile(
                candidate.Principal,
                GenericRead | GenericWrite,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                0,
                IntPtr.Zero);

            if (handle.IsInvalid || handle.IsClosed)
            {
                handle.Dispose();
                continue;
            }

            // The read is the compatibility gate. Unknown return encodings do not
            // select the backend and therefore can never reach SetAndVerify.
            if (TryGet(candidate, handle, out _))
            {
                _driver = candidate;
                _handle = handle;
                return;
            }

            handle.Dispose();
        }

        _ = EnsureVantageBackend();
    }

    private bool EnsureVantageBackend()
    {
        if (_vantage is not null && _vantage.TryGet(out _))
            return true;

        if (DateTimeOffset.UtcNow - _lastVantageProbe < TimeSpan.FromSeconds(15))
            return false;

        _lastVantageProbe = DateTimeOffset.UtcNow;
        _vantage = VantageKeyboardBackend.TryCreate();
        return _vantage is not null;
    }

    private static bool TryGet(
        DriverConfig driver,
        SafeFileHandle handle,
        out KeyboardBacklightLevel level)
    {
        level = KeyboardBacklightLevel.Off;
        byte[]? input = driver.GetInput.HasValue
            ? BitConverter.GetBytes(driver.GetInput.Value)
            : null;
        var output = new byte[16];

        if (!DeviceIoControl(
                handle,
                driver.GetIoctl,
                input,
                input?.Length ?? 0,
                output,
                output.Length,
                out int returned,
                IntPtr.Zero) || returned < 4)
        {
            return false;
        }

        uint raw = BitConverter.ToUInt32(output, 0);
        if (raw == driver.GetOff) level = KeyboardBacklightLevel.Off;
        else if (raw == driver.GetLow) level = KeyboardBacklightLevel.Low;
        else if (raw == driver.GetHigh) level = KeyboardBacklightLevel.High;
        else if (driver.GetAuto.HasValue && raw == driver.GetAuto.Value) level = KeyboardBacklightLevel.FirmwareAuto;
        else return false;

        return true;
    }

    /// <summary>
    /// Drops every cached Lenovo keyboard backend so an explicit Hardware Setup
    /// refresh really performs a fresh read-probe after a driver/Vantage repair.
    /// Normal status polling keeps using the validated backend and its normal backoff.
    /// </summary>
    public void RefreshBackend()
    {
        _handle?.Dispose();
        _handle = null;
        _driver = null;
        _vantage = null;
        _lastVantageProbe = DateTimeOffset.MinValue;
        _lastBackendLabel = null;
    }

    public void Dispose() => RefreshBackend();

    private sealed record DriverConfig(
        string Name,
        string Principal,
        uint GetIoctl,
        uint? GetInput,
        uint GetOff,
        uint GetLow,
        uint GetHigh,
        uint? GetAuto,
        uint SetIoctl,
        uint SetOff,
        uint SetLow,
        uint SetHigh);

    private sealed class VantageKeyboardBackend
    {
        private readonly object _control;
        private readonly MethodInfo _get;
        private readonly MethodInfo _set;

        private VantageKeyboardBackend(object control, MethodInfo get, MethodInfo set, string dllPath)
        {
            _control = control;
            _get = get;
            _set = set;
            Label = $"Lenovo Vantage · {Path.GetFileName(Path.GetDirectoryName(dllPath))}";
        }

        internal string Label { get; }

        internal static VantageKeyboardBackend? TryCreate()
        {
            foreach (string dllPath in EnumerateKeyboardCoreCandidates())
            {
                try
                {
                    string directory = Path.GetDirectoryName(dllPath) ?? string.Empty;
                    if (directory.Length == 0)
                        continue;

                    // Lenovo's recent ThinkKeyboardAddin builds have not kept
                    // assembly metadata consistent. The containing ProgramData path
                    // is Lenovo-owned, so blank metadata must not reject an OEM DLL.
                    FileVersionInfo info = FileVersionInfo.GetVersionInfo(dllPath);
                    string vendor = $"{info.CompanyName} {info.ProductName}".Trim();
                    if (!string.IsNullOrWhiteSpace(vendor) &&
                        !vendor.Contains("Lenovo", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string contractPath = Path.Combine(directory, "Contract_Keyboard.dll");
                    if (File.Exists(contractPath))
                    {
                        try { _ = Assembly.LoadFrom(contractPath); }
                        catch { }
                    }

                    Assembly assembly = Assembly.LoadFrom(dllPath);
                    LoadAdjacentManagedDependencies(assembly, directory);

                    Type? type = assembly.GetType("Keyboard_Core.KeyboardControl", throwOnError: false, ignoreCase: false);
                    if (type is null)
                        continue;

                    object? control = Activator.CreateInstance(type);
                    MethodInfo? get = type.GetMethod("GetKeyboardBackLightStatus", BindingFlags.Public | BindingFlags.Instance);
                    MethodInfo? set = type.GetMethod("SetKeyboardBackLightStatus", BindingFlags.Public | BindingFlags.Instance);
                    if (control is null || get is null || set is null)
                        continue;

                    var backend = new VantageKeyboardBackend(control, get, set, dllPath);
                    if (backend.TryGet(out _))
                        return backend;
                }
                catch
                {
                    // Installed Lenovo add-ins vary by generation. Failure to load
                    // one candidate is simply an unavailable fallback.
                }
            }

            return null;
        }

        private static IEnumerable<string> EnumerateKeyboardCoreCandidates()
        {
            var candidates = new List<string>();
            foreach (string root in VantageKeyboardRoots)
            {
                try
                {
                    if (!Directory.Exists(root))
                        continue;

                    // Newer Vantage/ImController packages may put the managed add-in
                    // under version/x64/bin layers rather than directly below root.
                    // Search only inside Lenovo's known keyboard-plugin roots.
                    candidates.AddRange(Directory.EnumerateFiles(
                        root,
                        "Keyboard_Core.dll",
                        SearchOption.AllDirectories));
                }
                catch
                {
                }
            }

            return candidates
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(path => path.Contains("\\x64\\", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(path =>
                {
                    try { return File.GetLastWriteTimeUtc(path); }
                    catch { return DateTime.MinValue; }
                });
        }

        private static void LoadAdjacentManagedDependencies(Assembly assembly, string directory)
        {
            foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
            {
                if (string.IsNullOrWhiteSpace(reference.Name) ||
                    AppDomain.CurrentDomain.GetAssemblies().Any(existing =>
                        string.Equals(existing.GetName().Name, reference.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                string candidate = Path.Combine(directory, reference.Name + ".dll");
                if (!File.Exists(candidate))
                    continue;

                try { _ = Assembly.LoadFrom(candidate); }
                catch (BadImageFormatException) { }
                catch (FileLoadException) { }
            }
        }

        internal bool TryGet(out KeyboardBacklightLevel level)
        {
            level = KeyboardBacklightLevel.Off;
            try
            {
                ParameterInfo[] parameters = _get.GetParameters();
                object?[] args = CreateInvocationArguments(parameters);
                object? result = _get.Invoke(_control, args);

                // Lenovo has shipped both ref/out and direct-return variants. Prefer
                // an actual by-ref/out status parameter; otherwise accept a direct
                // numeric/enum return. Never interpret a Boolean success return as a
                // backlight level.
                object? raw = null;
                int byRefIndex = Array.FindIndex(parameters, parameter =>
                    parameter.ParameterType.IsByRef || parameter.IsOut);
                if (byRefIndex >= 0 && byRefIndex < args.Length)
                    raw = args[byRefIndex];
                else if (result is not null && result is not bool)
                    raw = result;
                else if (args.Length > 0)
                    raw = args[0];

                if (!TryReadLevel(raw, out level))
                    return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal bool SetAndVerify(KeyboardBacklightLevel level)
        {
            try
            {
                ParameterInfo[] parameters = _set.GetParameters();
                if (parameters.Length < 1)
                    return false;

                object?[] args = CreateInvocationArguments(parameters);
                Type levelType = UnwrapByRef(parameters[0].ParameterType);
                args[0] = levelType.IsEnum
                    ? Enum.ToObject(levelType, (int)level)
                    : Convert.ChangeType((int)level, levelType);

                _ = _set.Invoke(_control, args);
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    Thread.Sleep(attempt == 0 ? 90 : 75);
                    if (TryGet(out KeyboardBacklightLevel current) && current == level)
                        return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadLevel(object? raw, out KeyboardBacklightLevel level)
        {
            level = KeyboardBacklightLevel.Off;
            try
            {
                int value = Convert.ToInt32(raw);
                if (value is < 0 or > 3)
                    return false;
                level = (KeyboardBacklightLevel)value;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static object?[] CreateInvocationArguments(ParameterInfo[] parameters)
        {
            var args = new object?[parameters.Length];
            for (int index = 0; index < parameters.Length; index++)
            {
                Type type = UnwrapByRef(parameters[index].ParameterType);
                args[index] = type.IsValueType ? Activator.CreateInstance(type) : null;
            }
            return args;
        }

        private static Type UnwrapByRef(Type type) =>
            type.IsByRef ? type.GetElementType() ?? typeof(object) : type;
    }
}
