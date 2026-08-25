using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace ThinkControl.UI.Services;

public sealed record DisplaySnapshot(
    int CurrentRefreshHz,
    IReadOnlyList<int> SupportedRefreshRates,
    int? Brightness,
    bool? AdaptiveBrightness);

public sealed class DisplayService
{
    private const int EnumCurrentSettings = -1;
    private const int DispChangeSuccessful = 0;
    private const int DmDisplayFrequency = 0x00400000;
    private static readonly TimeSpan CapabilityCacheLifetime = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan AdaptiveCacheLifetime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SnapshotCacheLifetime = TimeSpan.FromSeconds(12);

    private readonly object _cacheGate = new();
    private readonly object _snapshotGate = new();
    private IReadOnlyList<int> _cachedRates = Array.Empty<int>();
    private DateTimeOffset _ratesReadAt = DateTimeOffset.MinValue;
    private bool? _cachedAdaptive;
    private DateTimeOffset _adaptiveReadAt = DateTimeOffset.MinValue;
    private DisplaySnapshot? _cachedSnapshot;
    private DateTimeOffset _snapshotReadAt = DateTimeOffset.MinValue;

    public DisplaySnapshot Read()
    {
        lock (_snapshotGate)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (_cachedSnapshot is not null && now - _snapshotReadAt < SnapshotCacheLifetime)
                return _cachedSnapshot;
        }

        // Brightness uses WMI and adaptive-brightness discovery can invoke powercfg.
        // Those are capability/telemetry reads, not animation data. Sample them at a
        // human-scale cadence rather than every global UI refresh.
        var snapshot = new DisplaySnapshot(
            GetCurrentRefreshRate(),
            GetSupportedRefreshRates(),
            GetBrightness(),
            GetAdaptiveBrightness());

        lock (_snapshotGate)
        {
            _cachedSnapshot = snapshot;
            _snapshotReadAt = DateTimeOffset.UtcNow;
        }
        return snapshot;
    }

    public bool SetRefreshRate(int hz)
    {
        if (hz <= 0)
            return false;

        DEVMODE mode = CreateDevMode();
        if (!EnumDisplaySettings(null, EnumCurrentSettings, ref mode))
            return false;

        if (!GetSupportedRefreshRates().Contains(hz))
            return false;

        mode.dmDisplayFrequency = hz;
        mode.dmFields |= DmDisplayFrequency;
        bool changed = ChangeDisplaySettings(ref mode, 0) == DispChangeSuccessful;
        if (changed)
        {
            InvalidateCapabilities();
            InvalidateSnapshot();
        }
        return changed;
    }

    public bool SetMaximumRefreshRate()
    {
        int max = GetSupportedRefreshRates().DefaultIfEmpty(0).Max();
        return max > 0 && SetRefreshRate(max);
    }

    public int GetCurrentRefreshRate()
    {
        DEVMODE mode = CreateDevMode();
        return EnumDisplaySettings(null, EnumCurrentSettings, ref mode)
            ? mode.dmDisplayFrequency
            : 0;
    }

    public IReadOnlyList<int> GetSupportedRefreshRates()
    {
        lock (_cacheGate)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (_cachedRates.Count > 0 && now - _ratesReadAt < CapabilityCacheLifetime)
                return _cachedRates;

            _cachedRates = EnumerateSupportedRefreshRates();
            _ratesReadAt = now;
            return _cachedRates;
        }
    }

    private static IReadOnlyList<int> EnumerateSupportedRefreshRates()
    {
        DEVMODE current = CreateDevMode();
        if (!EnumDisplaySettings(null, EnumCurrentSettings, ref current))
            return [];

        var rates = new SortedSet<int>();
        int index = 0;
        while (true)
        {
            DEVMODE candidate = CreateDevMode();
            if (!EnumDisplaySettings(null, index++, ref candidate))
                break;

            if (candidate.dmPelsWidth == current.dmPelsWidth &&
                candidate.dmPelsHeight == current.dmPelsHeight &&
                candidate.dmDisplayFrequency is > 20 and < 1000)
            {
                rates.Add(candidate.dmDisplayFrequency);
            }
        }

        return rates.ToArray();
    }

    public int? GetBrightness()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                new ManagementScope(@"\\.\root\wmi"),
                new ObjectQuery("SELECT CurrentBrightness FROM WmiMonitorBrightness"));
            using ManagementObjectCollection results = searcher.Get();
            foreach (ManagementObject monitor in results)
                return Convert.ToInt32(monitor["CurrentBrightness"]);
        }
        catch (ManagementException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return null;
    }

    public bool SetBrightness(int value)
    {
        value = Math.Clamp(value, 0, 100);

        try
        {
            using var searcher = new ManagementObjectSearcher(
                new ManagementScope(@"\\.\root\wmi"),
                new ObjectQuery("SELECT * FROM WmiMonitorBrightnessMethods"));
            using ManagementObjectCollection results = searcher.Get();
            bool changed = false;
            foreach (ManagementObject monitor in results)
            {
                monitor.InvokeMethod("WmiSetBrightness", [1u, (byte)value]);
                changed = true;
            }

            if (changed)
            {
                lock (_snapshotGate)
                {
                    if (_cachedSnapshot is not null)
                        _cachedSnapshot = _cachedSnapshot with { Brightness = value };
                }
            }
            return changed;
        }
        catch (ManagementException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool? GetAdaptiveBrightness()
    {
        lock (_cacheGate)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (_adaptiveReadAt != DateTimeOffset.MinValue && now - _adaptiveReadAt < AdaptiveCacheLifetime)
                return _cachedAdaptive;

            _cachedAdaptive = ReadAdaptiveBrightness();
            _adaptiveReadAt = now;
            return _cachedAdaptive;
        }
    }

    private static bool? ReadAdaptiveBrightness()
    {
        ProcessResult result = RunPowerCfg("/Q SCHEME_CURRENT SUB_VIDEO ADAPTBRIGHT");
        if (result.ExitCode != 0)
            return null;

        MatchCollection matches = Regex.Matches(
            result.Output,
            @"Current (?:AC|DC) Power Setting Index:\s*0x(?<value>[0-9a-fA-F]+)",
            RegexOptions.IgnoreCase);
        if (matches.Count == 0)
            return null;

        return matches.Cast<Match>().Any(match =>
            int.TryParse(match.Groups["value"].Value, System.Globalization.NumberStyles.HexNumber, null, out int value) && value != 0);
    }

    public bool SetAdaptiveBrightness(bool enabled)
    {
        string value = enabled ? "1" : "0";
        ProcessResult ac = RunPowerCfg($"/SETACVALUEINDEX SCHEME_CURRENT SUB_VIDEO ADAPTBRIGHT {value}");
        ProcessResult dc = RunPowerCfg($"/SETDCVALUEINDEX SCHEME_CURRENT SUB_VIDEO ADAPTBRIGHT {value}");
        ProcessResult activate = RunPowerCfg("/SETACTIVE SCHEME_CURRENT");
        bool changed = ac.ExitCode == 0 && dc.ExitCode == 0 && activate.ExitCode == 0;
        if (changed)
        {
            lock (_cacheGate)
            {
                _cachedAdaptive = enabled;
                _adaptiveReadAt = DateTimeOffset.UtcNow;
            }
            lock (_snapshotGate)
            {
                if (_cachedSnapshot is not null)
                    _cachedSnapshot = _cachedSnapshot with { AdaptiveBrightness = enabled };
            }
        }
        return changed;
    }

    private void InvalidateCapabilities()
    {
        lock (_cacheGate)
        {
            _ratesReadAt = DateTimeOffset.MinValue;
            _cachedRates = Array.Empty<int>();
        }
    }

    private void InvalidateSnapshot()
    {
        lock (_snapshotGate)
        {
            _snapshotReadAt = DateTimeOffset.MinValue;
            _cachedSnapshot = null;
        }
    }

    private static ProcessResult RunPowerCfg(string arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit(3000);
            return new ProcessResult(process.HasExited ? process.ExitCode : -1, output + Environment.NewLine + error);
        }
        catch
        {
            return new ProcessResult(-1, string.Empty);
        }
    }

    private static DEVMODE CreateDevMode() => new()
    {
        dmSize = (short)Marshal.SizeOf<DEVMODE>()
    };

    private readonly record struct ProcessResult(int ExitCode, string Output);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ChangeDisplaySettings(ref DEVMODE devMode, int flags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        private const int CchDeviceName = 32;
        private const int CchFormName = 32;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchDeviceName)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchFormName)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }
}
