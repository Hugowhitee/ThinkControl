using System.ServiceProcess;

namespace ThinkControl.Service;

internal sealed class ThinkControlWindowsService : ServiceBase
{
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private ServiceEngine? _engine;

    internal ThinkControlWindowsService()
    {
        ServiceName = "ThinkControlService";
        CanStop = true;
        CanShutdown = true;
        CanPauseAndContinue = false;
        AutoLog = false;
    }

    protected override void OnStart(string[] args)
    {
        lock (_gate)
        {
            _cts = new CancellationTokenSource();
            _engine = new ServiceEngine();
            _runTask = Task.Run(() => _engine.RunAsync(_cts.Token));
            _ = ObserveRunTaskAsync(_runTask);
        }
        ServiceLog.Write("Windows service started.");
    }

    private async Task ObserveRunTaskAsync(Task runTask)
    {
        try
        {
            await runTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            ServiceLog.Write($"Fatal service engine failure: {ex.GetType().Name}: {ex.Message}");
            try { Stop(); } catch { }
        }
    }

    protected override void OnStop()
    {
        ServiceLog.Write("Windows service stopping.");
        StopEngine();
    }

    protected override void OnShutdown()
    {
        ServiceLog.Write("Windows shutdown requested service stop.");
        StopEngine();
    }

    private void StopEngine()
    {
        CancellationTokenSource? cts;
        Task? task;
        ServiceEngine? engine;

        lock (_gate)
        {
            cts = _cts;
            task = _runTask;
            engine = _engine;
            _cts = null;
            _runTask = null;
            _engine = null;
        }

        try { cts?.Cancel(); } catch { }
        try { task?.Wait(TimeSpan.FromSeconds(5)); } catch { }
        try { engine?.Dispose(); } catch { }
        cts?.Dispose();
    }
}
