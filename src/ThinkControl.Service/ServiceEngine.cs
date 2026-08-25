using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using ThinkControl.Core.Ipc;
using ThinkControl.Hardware.Lenovo;

namespace ThinkControl.Service;

internal sealed class ServiceEngine : IDisposable
{
    private const int MaxRequestBytes = 4096;
    private static readonly TimeSpan StatusRefreshInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StatusDemandHold = TimeSpan.FromSeconds(12);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly LenovoHardwareController _hardware = new();
    private readonly FanSupervisor _fanSupervisor;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly object _statusGate = new();
    private readonly SemaphoreSlim _statusWake = new(0, 1);

    private Task? _statusRefreshTask;
    private ServiceResponse? _lastStatus;
    private DateTimeOffset _statusDemandUntil = DateTimeOffset.MinValue;
    private bool _disposed;

    internal ServiceEngine()
    {
        _fanSupervisor = new FanSupervisor(_hardware);
    }

    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        CancellationToken token = linked.Token;

        // Create the IPC endpoint before starting any hardware discovery. A slow or
        // broken sensor provider must never make an otherwise healthy Windows service
        // look unreachable to the non-elevated UI.
        NamedPipeServerStream? nextServer = null;
        try
        {
            nextServer = CreateServerPipe();
            ServiceLog.Write($"IPC ready on {ThinkControlProtocol.PipeName}; hardware telemetry is demand-driven.");
        }
        catch (Exception ex)
        {
            ServiceLog.Write($"IPC startup failed: {ex.GetType().Name}: {ex.Message}");
            throw;
        }

        _fanSupervisor.Start(token);
        _statusRefreshTask = Task.Run(() => StatusRefreshLoopAsync(token), token);

        while (!token.IsCancellationRequested)
        {
            NamedPipeServerStream? server = nextServer;
            nextServer = null;
            try
            {
                server ??= CreateServerPipe();
                await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                // Prepare the next listener before servicing this client so a status
                // read cannot transiently block a Ping/repair verification request.
                try { nextServer = CreateServerPipe(); }
                catch (Exception ex)
                {
                    ServiceLog.Write($"Could not pre-create next IPC listener: {ex.GetType().Name}: {ex.Message}");
                }

                _ = HandleConnectionAsync(server, token);
                server = null;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                server?.Dispose();
                break;
            }
            catch (Exception ex)
            {
                server?.Dispose();
                ServiceLog.Write($"IPC loop recovered from {ex.GetType().Name}: {ex.Message}");
                try { await Task.Delay(250, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }

        nextServer?.Dispose();
    }

    private async Task StatusRefreshLoopAsync(CancellationToken token)
    {
        // Do one startup discovery so the first UI request can return useful state.
        // After that, LHM/PawnIO traversal sleeps completely unless a UI client has
        // requested telemetry recently. FanSupervisor owns its own safety loop while
        // manual/custom fan control is active, so tray-idle telemetry is unnecessary.
        bool initialDiscovery = true;
        while (!token.IsCancellationRequested)
        {
            bool demanded;
            lock (_statusGate)
                demanded = DateTimeOffset.UtcNow < _statusDemandUntil;

            if (!initialDiscovery && !demanded)
            {
                try { await _statusWake.WaitAsync(token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                continue;
            }

            initialDiscovery = false;
            try
            {
                ServiceResponse status = BuildStatusResponse();
                lock (_statusGate) _lastStatus = status;
            }
            catch (Exception ex)
            {
                lock (_statusGate)
                    _lastStatus = ProviderDiscoveryResponse($"Provider refresh failed safely: {ex.Message}");
            }

            try
            {
                Task delay = Task.Delay(StatusRefreshInterval, token);
                Task wake = _statusWake.WaitAsync(token);
                await Task.WhenAny(delay, wake).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void SignalStatusDemand()
    {
        lock (_statusGate)
            _statusDemandUntil = DateTimeOffset.UtcNow + StatusDemandHold;

        if (_statusWake.CurrentCount == 0)
        {
            try { _statusWake.Release(); }
            catch (SemaphoreFullException) { }
        }
    }

    private static NamedPipeServerStream CreateServerPipe()
    {
        var security = new PipeSecurity();
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var interactive = new SecurityIdentifier(WellKnownSidType.InteractiveSid, null);

        security.SetOwner(system);
        security.AddAccessRule(new PipeAccessRule(system, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(admins, PipeAccessRights.FullControl, AccessControlType.Allow));

        security.AddAccessRule(new PipeAccessRule(
            interactive,
            PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            ThinkControlProtocol.PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            MaxRequestBytes,
            MaxRequestBytes,
            security,
            HandleInheritability.None);
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        using (pipe)
        {
            try
            {
                using var reader = new StreamReader(pipe, Encoding.UTF8, false, MaxRequestBytes, leaveOpen: true);
                string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null || Encoding.UTF8.GetByteCount(line) > MaxRequestBytes)
                {
                    await WriteAsync(pipe, Error("Invalid or oversized request."), cancellationToken).ConfigureAwait(false);
                    return;
                }

                ServiceRequest? request;
                try
                {
                    request = JsonSerializer.Deserialize<ServiceRequest>(line, JsonOptions);
                }
                catch (JsonException)
                {
                    await WriteAsync(pipe, Error("Malformed JSON request."), cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (request is null || request.Version != ThinkControlProtocol.Version)
                {
                    await WriteAsync(pipe, Error("Unsupported ThinkControl protocol version."), cancellationToken).ConfigureAwait(false);
                    return;
                }

                ServiceResponse response = HandleRequest(request);
                await WriteAsync(pipe, response, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (IOException)
            {
            }
        }
    }

    private ServiceResponse HandleRequest(ServiceRequest request)
    {
        try
        {
            return request.Operation switch
            {
                "Ping" => new ServiceResponse(ThinkControlProtocol.Version, true),
                "GetStatus" => GetCachedStatusAndRequestRefresh(),
                "RefreshProviders" => RefreshProviders(),
                "SetFanLevel" => SetFanLevel(request.Value),
                "ReturnFanToAuto" => ReturnFanToAuto(),
                "SetCoolingProfile" => SetCoolingProfile(request.Value),
                "StartFanCharacterization" => StartFanCharacterization(),
                "MarkFanLevelAudible" => MarkFanLevelAudible(),
                "StopFanCharacterization" => StopFanCharacterization(),
                "SetKeyboardBacklight" => SetKeyboardBacklight(request.Value),
                "SetThermalMode" => SetThermalMode(request.Value),
                _ => Error("Unsupported operation. Raw EC, port and IOCTL passthrough are never exposed by ThinkControl.")
            };
        }
        catch (Exception ex)
        {
            return Error($"Hardware operation failed safely: {ex.Message}");
        }
    }

    private ServiceResponse GetCachedStatusAndRequestRefresh()
    {
        SignalStatusDemand();
        return CachedStatusResponse();
    }

    private ServiceResponse CachedStatusResponse()
    {
        lock (_statusGate)
            return _lastStatus ?? ProviderDiscoveryResponse("Service online · detecting hardware providers");
    }

    private ServiceResponse RefreshProviders()
    {
        CoolingSupervisorSnapshot cooling = _fanSupervisor.Snapshot();
        if (cooling.Characterization.Running)
            return Error("Stop fan characterization before refreshing hardware providers.");

        bool thinkControlOwnsFan = cooling.AppliedLevel.HasValue ||
            !cooling.Profile.Equals("Lenovo Auto", StringComparison.OrdinalIgnoreCase);
        if (thinkControlOwnsFan && !_fanSupervisor.ReturnToAuto(out string? handoffError))
        {
            return Error(
                "Provider refresh was blocked because ThinkControl could not safely return fan ownership to Lenovo Auto. " +
                (handoffError ?? "Retry Lenovo Auto, then refresh providers again."));
        }

        _hardware.RefreshProviders();
        ServiceResponse discovering = ProviderDiscoveryResponse("Providers recycled · re-detecting PawnIO/LHM, X9 EC and Lenovo keyboard backends");
        lock (_statusGate) _lastStatus = discovering;
        SignalStatusDemand();
        return discovering;
    }

    private ServiceResponse BuildStatusResponse()
    {
        LenovoHardwareStatus status = _hardware.ReadStatus();
        CoolingSupervisorSnapshot cooling = _fanSupervisor.Snapshot();
        FanTelemetrySnapshot[] fans = status.Fans
            .Select((fan, index) => new FanTelemetrySnapshot(
                fan.Id,
                fan.Label,
                fan.Rpm,
                fan.Source,
                Primary: index == 0))
            .ToArray();

        HardwareSensorSnapshot[] sensors = status.Sensors
            .Select(sensor => new HardwareSensorSnapshot(
                sensor.Id,
                sensor.HardwareName,
                sensor.HardwareType,
                sensor.Name,
                sensor.SensorType,
                sensor.Value,
                sensor.Unit,
                sensor.ControlTemperature,
                sensor.Source))
            .ToArray();

        var telemetry = new TelemetrySnapshot(
            status.CpuTemperatureC,
            status.CpuTemperatureSource,
            status.FanRpm,
            status.FanRpmSource,
            status.FanState,
            status.HardwareAccess,
            status.KeyboardBacklight,
            ThermalSolutionVersion: null,
            Fans: fans,
            Sensors: sensors,
            ControlTemperatureC: status.ControlTemperatureC,
            ControlTemperatureSource: status.ControlTemperatureSource,
            CoolingProfile: cooling.Profile,
            CoolingAppliedLevel: cooling.AppliedLevel,
            CoolingSmoothedTemperatureC: cooling.SmoothedTemperatureC,
            CoolingStatus: cooling.Status,
            CoolingSafetyOverride: cooling.SafetyOverride,
            FanCharacterization: cooling.Characterization);

        var capabilities = new HardwareCapabilitySnapshot(
            status.CanFanTelemetry,
            status.CanFanControl,
            status.CanKeyboardBacklight,
            status.CanCpuTemperature,
            SensorTelemetry: status.CanSensorTelemetry,
            FanCount: fans.Length);

        return new ServiceResponse(
            ThinkControlProtocol.Version,
            true,
            Telemetry: telemetry,
            Capabilities: capabilities);
    }

    private ServiceResponse RefreshAndReturnStatus()
    {
        ServiceResponse status = BuildStatusResponse();
        lock (_statusGate) _lastStatus = status;
        return status;
    }

    private static ServiceResponse ProviderDiscoveryResponse(string detail)
    {
        var telemetry = new TelemetrySnapshot(
            null,
            "Detecting",
            null,
            "Detecting",
            "Lenovo managed · provider discovery in progress",
            detail,
            "Detecting…",
            Fans: Array.Empty<FanTelemetrySnapshot>(),
            Sensors: Array.Empty<HardwareSensorSnapshot>(),
            CoolingStatus: "Lenovo firmware owns fan control while providers are detected");
        var capabilities = new HardwareCapabilitySnapshot(false, false, false, false, false, 0);
        return new ServiceResponse(ThinkControlProtocol.Version, true, Telemetry: telemetry, Capabilities: capabilities);
    }

    private ServiceResponse SetFanLevel(string? raw)
    {
        if (!int.TryParse(raw, out int level))
            return Error("Fan level is missing or invalid.");

        return _fanSupervisor.SetManualLevel(level, out string? error)
            ? RefreshAndReturnStatus()
            : Error(error ?? "Fan level rejected.");
    }

    private ServiceResponse ReturnFanToAuto() =>
        _fanSupervisor.ReturnToAuto(out string? error)
            ? RefreshAndReturnStatus()
            : Error(error ?? "Lenovo Auto rejected.");

    private ServiceResponse SetCoolingProfile(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Error("Cooling profile is missing.");
        return _fanSupervisor.SetProfile(value, out string? error)
            ? RefreshAndReturnStatus()
            : Error(error ?? "Cooling profile rejected.");
    }

    private ServiceResponse StartFanCharacterization() =>
        _fanSupervisor.StartCharacterization(out string? error)
            ? RefreshAndReturnStatus()
            : Error(error ?? "Fan characterization could not start.");

    private ServiceResponse MarkFanLevelAudible() =>
        _fanSupervisor.MarkCurrentLevelAudible(out string? error)
            ? RefreshAndReturnStatus()
            : Error(error ?? "Audible fan level could not be recorded.");

    private ServiceResponse StopFanCharacterization() =>
        _fanSupervisor.StopCharacterization(out string? error)
            ? RefreshAndReturnStatus()
            : Error(error ?? "Fan characterization could not stop.");

    private ServiceResponse SetKeyboardBacklight(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Error("Keyboard level is missing.");

        return _hardware.SetKeyboardBacklight(value, out string? error)
            ? RefreshAndReturnStatus()
            : Error(error ?? "Keyboard level rejected.");
    }

    private ServiceResponse SetThermalMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Error("Thermal mode is missing.");

        return LenovoThermalPolicyService.TrySetX9Policy(_hardware.Identity, value, out string? detail)
            ? new ServiceResponse(ThinkControlProtocol.Version, true, detail)
            : Error(detail ?? "Lenovo thermal policy rejected the request.");
    }

    private static ServiceResponse Error(string message) =>
        new(ThinkControlProtocol.Version, false, message);

    private static async Task WriteAsync(
        NamedPipeServerStream pipe,
        ServiceResponse response,
        CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(response, JsonOptions) + "\n";
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        await pipe.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _disposeCts.Cancel();
        try { _statusWake.Release(); } catch { }
        try { _statusRefreshTask?.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _fanSupervisor.Dispose();
        _hardware.Dispose();
        _statusWake.Dispose();
        _disposeCts.Dispose();
    }
}
