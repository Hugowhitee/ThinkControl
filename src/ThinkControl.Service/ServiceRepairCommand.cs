using System.Diagnostics;
using System.IO.Pipes;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using ThinkControl.Core.Ipc;

namespace ThinkControl.Service;

internal static class ServiceRepairCommand
{
    private const string ServiceName = "ThinkControlService";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal static int Run()
    {
        string? executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
            return 20;

        try
        {
            bool exists = ServiceExists();
            if (exists)
            {
                _ = RunSc("stop", ServiceName);
                WaitFor(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));

                int config = RunSc(
                    "config", ServiceName,
                    "binPath=", $"\"{executable}\"",
                    "start=", "auto",
                    "DisplayName=", "ThinkControl Hardware Service");
                if (config != 0)
                    return 21;
            }
            else
            {
                int create = RunSc(
                    "create", ServiceName,
                    "binPath=", $"\"{executable}\"",
                    "start=", "auto",
                    "DisplayName=", "ThinkControl Hardware Service");
                if (create != 0)
                    return 22;
            }

            _ = RunSc(
                "description", ServiceName,
                "Verified ThinkControl hardware access service");
            _ = RunSc(
                "failure", ServiceName,
                "reset=", "86400",
                "actions=", "restart/5000");

            int start = RunSc("start", ServiceName);
            if (start != 0 && !IsRunning())
                return 23;

            if (!WaitFor(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10)))
                return 24;

            // SCM reporting RUNNING is not enough. The regression that prompted this
            // repair path was exactly a service process that stayed registered/running
            // while the user-session pipe was unusable. Verify the same Ping protocol
            // the UI uses before reporting a successful repair.
            if (!WaitForResponsivePipe(TimeSpan.FromSeconds(10)))
            {
                ServiceLog.Write("Repair started the service, but IPC Ping never became responsive.");
                return 26;
            }

            ServiceLog.Write("Repair verified SCM running state and user-session IPC Ping.");
            return 0;
        }
        catch (Exception ex)
        {
            ServiceLog.Write($"Repair command failed: {ex.GetType().Name}: {ex.Message}");
            return 25;
        }
    }

    private static bool WaitForResponsivePipe(TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (TryPingPipe())
                return true;
            Thread.Sleep(250);
        }
        return false;
    }

    private static bool TryPingPipe()
    {
        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                ThinkControlProtocol.PipeName,
                PipeDirection.InOut,
                PipeOptions.None);
            pipe.Connect(450);
            if (!pipe.IsConnected)
                return false;

            string json = JsonSerializer.Serialize(
                new ServiceRequest(ThinkControlProtocol.Version, "Ping"),
                JsonOptions) + "\n";
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            pipe.Write(bytes, 0, bytes.Length);
            pipe.Flush();

            using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, leaveOpen: true);
            Task<string?> read = reader.ReadLineAsync();
            if (!read.Wait(TimeSpan.FromMilliseconds(700)))
                return false;
            string? line = read.Result;
            if (string.IsNullOrWhiteSpace(line))
                return false;

            ServiceResponse? response = JsonSerializer.Deserialize<ServiceResponse>(line, JsonOptions);
            return response?.Success == true;
        }
        catch
        {
            return false;
        }
    }

    private static bool ServiceExists()
    {
        try
        {
            using var service = new ServiceController(ServiceName);
            _ = service.Status;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool IsRunning()
    {
        try
        {
            using var service = new ServiceController(ServiceName);
            service.Refresh();
            return service.Status == ServiceControllerStatus.Running;
        }
        catch
        {
            return false;
        }
    }

    private static bool WaitFor(ServiceControllerStatus desired, TimeSpan timeout)
    {
        try
        {
            using var service = new ServiceController(ServiceName);
            service.WaitForStatus(desired, timeout);
            service.Refresh();
            return service.Status == desired;
        }
        catch
        {
            return desired == ServiceControllerStatus.Stopped;
        }
    }

    private static int RunSc(params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "sc.exe"),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        foreach (string argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);

        process.Start();
        _ = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(15_000))
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return -1;
        }

        return process.ExitCode;
    }
}
