using System.ServiceProcess;

namespace ThinkControl.Service;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Any(arg => string.Equals(arg, "--repair-service", StringComparison.OrdinalIgnoreCase)))
            return ServiceRepairCommand.Run();

        bool console = args.Any(arg => string.Equals(arg, "--console", StringComparison.OrdinalIgnoreCase));

        if (!Environment.UserInteractive && !console)
        {
            ServiceBase.Run(new ThinkControlWindowsService());
            return 0;
        }

        return RunConsoleAsync().GetAwaiter().GetResult();
    }

    private static async Task<int> RunConsoleAsync()
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        using var engine = new ServiceEngine();
        Console.WriteLine("ThinkControl.Service running in console mode. Ctrl+C to stop.");
        await engine.RunAsync(cts.Token).ConfigureAwait(false);
        return 0;
    }
}
