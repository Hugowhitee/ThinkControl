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
        }
    }

    protected override void OnStop() => StopEngine();

    protected override void OnShutdown() => StopEngine();

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
