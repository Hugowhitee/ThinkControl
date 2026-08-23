using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using ThinkControl.Core.Ipc;

namespace ThinkControl.UI.Services;

public sealed record HardwareOperationResult(
    string Operation,
    string? Value,
    bool Success,
    int DurationMs,
    bool ResponseReceived);

public sealed class HardwareServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private ServiceResponse? _lastValidStatus;

    public event EventHandler<HardwareOperationResult>? HardwareOperationCompleted;
    public event EventHandler<ServiceResponse?>? StatusObserved;

    public async Task<ServiceResponse?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        ServiceResponse? response = await GetStatusCoreAsync(cancellationToken);
        try { StatusObserved?.Invoke(this, response); }
        catch { }
        return response;
    }

    private async Task<ServiceResponse?> GetStatusCoreAsync(CancellationToken cancellationToken)
    {
        // A cold status read can initialize LibreHardwareMonitor, Lenovo WMI,
        // keyboard providers and the verified EC backend. Keep this generous on
        // first contact; the call is asynchronous and interactive writes use much
        // tighter timeouts.
        ServiceRequest request = new(ThinkControlProtocol.Version, "GetStatus");
        ServiceResponse? response = await SendAsync(request, cancellationToken, timeoutMs: 4200);
        if (IsValidStatus(response))
        {
            _lastValidStatus = response;
            return response;
        }
        if (response is not null || cancellationToken.IsCancellationRequested)
            return response;

        try
        {
            await Task.Delay(140, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        response = await SendAsync(request, cancellationToken, timeoutMs: 2400);
        if (IsValidStatus(response))
        {
            _lastValidStatus = response;
            return response;
        }
        if (response is not null || cancellationToken.IsCancellationRequested)
            return response;

        // A slow provider refresh is not the same as a dead Windows service.
        // Ping is provider-free and fast. Reuse the last complete snapshot only
        // when the service itself is demonstrably still alive.
        if (await PingAsync(cancellationToken).ConfigureAwait(false) && _lastValidStatus is not null)
        {
            return _lastValidStatus with
            {
                Error = "Hardware service is online; provider refresh timed out. Showing the last complete status."
            };
        }

        return null;
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        ServiceResponse? response = await SendAsync(
            new ServiceRequest(ThinkControlProtocol.Version, "Ping"),
            cancellationToken,
            timeoutMs: 500);
        return response?.Success == true;
    }

    public async Task<ServiceResponse?> SetFanLevelAsync(int level, CancellationToken cancellationToken = default) =>
        await SendTrackedAsync("SetFanLevel", level.ToString(), cancellationToken);

    public async Task<ServiceResponse?> ReturnFanToAutoAsync(CancellationToken cancellationToken = default) =>
        await SendTrackedAsync("ReturnFanToAuto", null, cancellationToken);

    public async Task<ServiceResponse?> SetKeyboardBacklightAsync(string value, CancellationToken cancellationToken = default) =>
        await SendAsync(new ServiceRequest(ThinkControlProtocol.Version, "SetKeyboardBacklight", value), cancellationToken, timeoutMs: 1400);

    public async Task<ServiceResponse?> SetThermalModeAsync(string value, CancellationToken cancellationToken = default) =>
        await SendTrackedAsync("SetThermalMode", value, cancellationToken, timeoutMs: 3200);

    private async Task<ServiceResponse?> SendTrackedAsync(
        string operation,
        string? value,
        CancellationToken cancellationToken,
        int timeoutMs = 650)
    {
        var stopwatch = Stopwatch.StartNew();
        ServiceResponse? response = await SendAsync(
            new ServiceRequest(ThinkControlProtocol.Version, operation, value),
            cancellationToken,
            timeoutMs);
        stopwatch.Stop();

        try
        {
            HardwareOperationCompleted?.Invoke(this, new HardwareOperationResult(
                operation,
                value,
                response?.Success == true,
                (int)Math.Clamp(stopwatch.ElapsedMilliseconds, 0, 600_000),
                response is not null));
        }
        catch
        {
        }

        return response;
    }

    private static bool IsValidStatus(ServiceResponse? response) =>
        response?.Success == true && response.Telemetry is not null;

    private static async Task<ServiceResponse?> SendAsync(
        ServiceRequest request,
        CancellationToken cancellationToken,
        int timeoutMs = 450)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                ThinkControlProtocol.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
            await pipe.ConnectAsync(timeoutCts.Token);

            string requestJson = JsonSerializer.Serialize(request, JsonOptions) + "\n";
            byte[] requestBytes = Encoding.UTF8.GetBytes(requestJson);
            await pipe.WriteAsync(requestBytes, timeoutCts.Token);
            await pipe.FlushAsync(timeoutCts.Token);

            using var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, leaveOpen: true);
            string? responseLine = await reader.ReadLineAsync(timeoutCts.Token);
            return string.IsNullOrWhiteSpace(responseLine)
                ? null
                : JsonSerializer.Deserialize<ServiceResponse>(responseLine, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
