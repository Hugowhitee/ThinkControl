using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace ThinkControl.Hardware.Lenovo;

/// <summary>
/// X9-only bridge to Lenovo Intelligent Thermal Solution's observed
/// ThinkSmartSense named-pipe contract. This is a thermal-policy interface,
/// not a direct fan-RPM/PWM API.
/// </summary>
public static class LenovoThermalPolicyService
{
    private const string PipeName = "com.lenovo.its.pipe.setting";
    private const int ConnectTimeoutMs = 900;
    private static readonly TimeSpan IoTimeout = TimeSpan.FromMilliseconds(1200);

    public static bool TrySetX9Policy(
        HardwareDeviceIdentity identity,
        string? requestedMode,
        out string? detail)
    {
        detail = null;

        if (!identity.IsVerifiedX9)
        {
            detail = "Lenovo Intelligent Cooling commands are restricted to the verified X9 21Q6/21Q7 profile.";
            return false;
        }

        if (!TryNormalizeMode(requestedMode, out string mode))
        {
            detail = "Thermal mode must be Quiet, Balanced or Performance.";
            return false;
        }

        if (!TryGetAcState(out bool onAc))
        {
            detail = "Windows power-source state could not be read.";
            return false;
        }

        uint command = (onAc, mode) switch
        {
            (true, "Quiet") => 502u,
            (true, "Balanced") => 503u,
            (true, "Performance") => 504u,
            (false, "Quiet") => 507u,
            (false, "Balanced") => 508u,
            (false, "Performance") => 509u,
            _ => 0u
        };

        if (command == 0)
        {
            detail = "No verified Lenovo thermal-policy command matches the request.";
            return false;
        }

        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            pipe.Connect(ConnectTimeoutMs);

            byte[] request = BitConverter.GetBytes(command);
            byte[] response = new byte[sizeof(int)];

            using var cts = new CancellationTokenSource(IoTimeout);
            pipe.WriteAsync(request.AsMemory(), cts.Token).AsTask().GetAwaiter().GetResult();
            pipe.FlushAsync(cts.Token).GetAwaiter().GetResult();

            int read = 0;
            while (read < response.Length)
            {
                int count = pipe.ReadAsync(response.AsMemory(read, response.Length - read), cts.Token)
                    .AsTask().GetAwaiter().GetResult();
                if (count <= 0)
                    break;
                read += count;
            }

            if (read != response.Length)
            {
                detail = $"LITSSvc accepted command {command} but did not return its complete Int32 response.";
                return false;
            }

            int result = BitConverter.ToInt32(response, 0);
            detail = $"LITSSvc command {command} ({mode}, {(onAc ? "AC" : "DC")}) returned {result}.";

            // The observed Lenovo contract is request/Int32-response. Lenovo has
            // not published response-value semantics, so receiving the complete
            // response is the readback boundary; do not invent a 0/nonzero rule.
            return true;
        }
        catch (TimeoutException)
        {
            detail = "Lenovo Intelligent Thermal Solution is installed but its policy pipe did not respond in time.";
            return false;
        }
        catch (OperationCanceledException)
        {
            detail = "Lenovo Intelligent Thermal Solution policy I/O timed out.";
            return false;
        }
        catch (IOException ex)
        {
            detail = $"Lenovo Intelligent Thermal Solution policy pipe is unavailable: {ex.Message}";
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            detail = "Lenovo Intelligent Thermal Solution rejected access to its policy pipe.";
            return false;
        }
    }

    private static bool TryNormalizeMode(string? value, out string mode)
    {
        mode = value?.Trim() switch
        {
            var raw when raw?.Equals("Quiet", StringComparison.OrdinalIgnoreCase) == true => "Quiet",
            var raw when raw?.Equals("Balanced", StringComparison.OrdinalIgnoreCase) == true => "Balanced",
            var raw when raw?.Equals("Performance", StringComparison.OrdinalIgnoreCase) == true => "Performance",
            _ => string.Empty
        };
        return mode.Length > 0;
    }

    private static bool TryGetAcState(out bool onAc)
    {
        onAc = false;
        if (!GetSystemPowerStatus(out SystemPowerStatus status))
            return false;

        // 0 = offline, 1 = online, 255 = unknown.
        if (status.AcLineStatus == 255)
            return false;

        onAc = status.AcLineStatus == 1;
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus systemPowerStatus);
}
