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
    private static readonly TimeSpan LastKnownGoodGrace = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan StatusEventInterval = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan OfflineRetryInterval = TimeSpan.FromSeconds(12);

    private ServiceResponse? _lastValidStatus;
    private DateTimeOffset _lastValidStatusAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastStatusEventAt = DateTimeOffset.MinValue;
    private DateTimeOffset _offlineRetryAfter = DateTimeOffset.MinValue;
    private bool _lastObservedOnline;

    public event EventHandler<HardwareOperationResult>? HardwareOperationCompleted;
    public event EventHandler<ServiceResponse?>? StatusObserved;

    public async Task<ServiceResponse?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        ServiceResponse? response = await GetStatusCoreAsync(cancellationToken).ConfigureAwait(false);
        PublishStatusIfNeeded(response);
        return response;
    }

    private async Task<ServiceResponse?> GetStatusCoreAsync(CancellationToken cancellationToken)
    {
        // When the Windows service is actually stopped, repeatedly creating a named
        // pipe client every UI refresh just burns kernel time and cannot make the
        // service recover. Preserve a very recent good snapshot for a short grace
        // period, but otherwise wait for the bounded retry or an explicit repair.
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (now < _offlineRetryAfter)
        {
            if (_lastValidStatus is not null && now - _lastValidStatusAt <= LastKnownGoodGrace)
            {
                return _lastValidStatus with
                {
                    Error = "Hardware service response was briefly delayed; showing the last known good snapshot."
                };
            }
            return null;
        }

        ServiceRequest request = new(ThinkControlProtocol.Version, "GetStatus");
        ServiceResponse? response = await SendAsync(request, cancellationToken, timeoutMs: 900).ConfigureAwait(false);
        if (IsValidStatus(response))
        {
            RememberValidStatus(response!);
            return response;
        }

        if (response is not null || cancellationToken.IsCancellationRequested)
            return response;

        bool online = await PingAsync(cancellationToken).ConfigureAwait(false);
        if (online)
        {
            _offlineRetryAfter = DateTimeOffset.MinValue;
            if (_lastValidStatus is not null)
            {
                return _lastValidStatus with
                {
                    Error = "Hardware service is online; showing the last complete provider snapshot while status refresh catches up."
                };
            }

            return OnlineDiscoveryResponse();
        }

        now = DateTimeOffset.UtcNow;
        _offlineRetryAfter = now + OfflineRetryInterval;
        if (_lastValidStatus is not null && now - _lastValidStatusAt <= LastKnownGoodGrace)
        {
            return _lastValidStatus with
            {
                Error = "Hardware service response was briefly delayed; showing the last known good snapshot."
            };
        }

        return null;
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        ServiceResponse? response = await SendAsync(
            new ServiceRequest(ThinkControlProtocol.Version, "Ping"),
            cancellationToken,
            timeoutMs: 350).ConfigureAwait(false);
        return response?.Success == true;
    }

    public async Task<ServiceResponse?> RefreshProvidersAsync(CancellationToken cancellationToken = default) =>
        await SendTrackedAsync("RefreshProviders", null, cancellationToken, timeoutMs: 1800);

    public async Task<ServiceResponse?> SetFanLevelAsync(int level, CancellationToken cancellationToken = default) =>
        await SendTrackedAsync("SetFanLevel", level.ToString(), cancellationToken);

    public async Task<ServiceResponse?> ReturnFanToAutoAsync(CancellationToken cancellationToken = default) =>
        await SendTrackedAsync("ReturnFanToAuto", null, cancellationToken);

    public async Task<ServiceResponse?> SetCoolingProfileAsync(string profile, CancellationToken cancellationToken = default) =>
        await SendTrackedAsync("SetCoolingProfile", profile, cancellationToken, timeoutMs: 1800);

    public async Task<ServiceResponse?> StartFanCharacterizationAsync(CancellationToken cancellationToken = default) =>
        await SendTrackedAsync("StartFanCharacterization", null, cancellationToken, timeoutMs: 1800);

    public async Task<ServiceResponse?> MarkFanLevelAudibleAsync(CancellationToken cancellationToken = default) =>
        await SendTrackedAsync("MarkFanLevelAudible", null, cancellationToken, timeoutMs: 1200);

    public async Task<ServiceResponse?> StopFanCharacterizationAsync(CancellationToken cancellationToken = default) =>
        await SendTrackedAsync("StopFanCharacterization", null, cancellationToken, timeoutMs: 1800);

    public async Task<ServiceResponse?> SetKeyboardBacklightAsync(string value, CancellationToken cancellationToken = default) =>
        await SendTrackedAsync("SetKeyboardBacklight", value, cancellationToken, timeoutMs: 1400);

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
            timeoutMs).ConfigureAwait(false);
        stopwatch.Stop();

        if (IsValidStatus(response))
        {
            RememberValidStatus(response!);
            PublishStatusIfNeeded(response, force: true);
        }

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

    private void RememberValidStatus(ServiceResponse response)
    {
        _lastValidStatus = response;
        _lastValidStatusAt = DateTimeOffset.UtcNow;
        _offlineRetryAfter = DateTimeOffset.MinValue;
    }

    private void PublishStatusIfNeeded(ServiceResponse? response, bool force = false)
    {
        bool online = IsValidStatus(response);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        bool stateChanged = online != _lastObservedOnline;
        if (!force && !stateChanged && now - _lastStatusEventAt < StatusEventInterval)
            return;

        _lastObservedOnline = online;
        _lastStatusEventAt = now;
        try { StatusObserved?.Invoke(this, response); }
        catch { }
    }

    private static bool IsValidStatus(ServiceResponse? response) =>
        response?.Success == true && response.Telemetry is not null;

    private static ServiceResponse OnlineDiscoveryResponse()
    {
        var telemetry = new TelemetrySnapshot(
            null,
            "Detecting",
            null,
            "Detecting",
            "Firmware/provider managed · discovery in progress",
            "Hardware service online · detecting hardware providers",
            "Detecting…",
            Fans: Array.Empty<FanTelemetrySnapshot>(),
            Sensors: Array.Empty<HardwareSensorSnapshot>(),
            CoolingStatus: "Firmware/OEM cooling control remains active while providers are detected");
        var capabilities = new HardwareCapabilitySnapshot(false, false, false, false, false, 0);
        return new ServiceResponse(
            ThinkControlProtocol.Version,
            true,
            "Hardware service is reachable; provider snapshot is still initializing.",
            telemetry,
            capabilities);
    }

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
            await pipe.ConnectAsync(timeoutCts.Token).ConfigureAwait(false);

            string requestJson = JsonSerializer.Serialize(request, JsonOptions) + "\n";
            byte[] requestBytes = Encoding.UTF8.GetBytes(requestJson);
            await pipe.WriteAsync(requestBytes, timeoutCts.Token).ConfigureAwait(false);
            await pipe.FlushAsync(timeoutCts.Token).ConfigureAwait(false);

            using var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, leaveOpen: true);
            string? responseLine = await reader.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false);
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
