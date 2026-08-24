using System.Diagnostics;
using System.ServiceProcess;

namespace ThinkControl.Service;

internal static class ServiceRepairCommand
{
    private const string ServiceName = "ThinkControlService";

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

            return WaitFor(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10)) ? 0 : 24;
        }
        catch
        {
            return 25;
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
