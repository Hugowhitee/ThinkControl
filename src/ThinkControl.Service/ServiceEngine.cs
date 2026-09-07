using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using ThinkControl.Core.Cooling;
using ThinkControl.Core.Ipc;
using ThinkControl.Hardware.Lenovo;

namespace ThinkControl.Service;

internal sealed class ServiceEngine : IDisposable
{
    private const int MaxRequestBytes = 8192;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan BatteryProtectionProbeInterval = TimeSpan.FromSeconds(30);

    private readonly LenovoHardwareController _hardware = new();
    private readonly FanSupervisor _fanSupervisor;
    private readonly LenovoCoolingPolicyCoordinator _coolingPolicy;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly object _statusGate = new();
    private readonly object _batteryProtectionGate = new();
    private readonly SemaphoreSlim _statusWake = new(0, 1);
    private Task? _statusRefreshTask;
    private ServiceResponse? _lastStatus;
    private LenovoBatteryChargeProtectionStatus? _batteryProtectionStatus;
    private DateTimeOffset _batteryProtectionRefreshAfter = DateTimeOffset.MinValue;
    private bool _disposed;

    internal ServiceEngine()
    {
        _fanSupervisor = new FanSupervisor(_hardware);
        _coolingPolicy = new LenovoCoolingPolicyCoordinator(_hardware);
    }

    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        CancellationToken token = linked.Token;
        NamedPipeServerStream? nextServer = null;

        try
        {
            nextServer = CreateServerPipe();
            ServiceLog.Write($"IPC ready on {ThinkControlProtocol.PipeName}; hardware telemetry is request-driven.");
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
                try { nextServer = CreateServerPipe(); }
                catch (Exception ex) { ServiceLog.Write($"Could not pre-create next IPC listener: {ex.GetType().Name}: {ex.Message}"); }
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
        bool initialDiscovery = true;
        while (!token.IsCancellationRequested)
        {
            if (!initialDiscovery)
            {
                try { await _statusWake.WaitAsync(token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
            initialDiscovery = false;

            try
            {
                ServiceResponse status = BuildStatusResponse();
                lock (_statusGate) _lastStatus = status;
            }
            catch (Exception ex)
            {
                lock (_statusGate) _lastStatus = ProviderDiscoveryResponse($"Provider refresh failed safely: {ex.Message}");
            }
        }
    }

    private void SignalStatusDemand()
    {
        if (_statusWake.CurrentCount != 0)
            return;
        try { _statusWake.Release(); }
        catch (SemaphoreFullException) { }
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
        security.AddAccessRule(new PipeAccessRule(interactive, PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize, AccessControlType.Allow));

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
                try { request = JsonSerializer.Deserialize<ServiceRequest>(line, JsonOptions); }
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

                await WriteAsync(pipe, HandleRequest(request), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (IOException) { }
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
                "RefreshSensorProviders" => RefreshSensorProviders(),
                "RefreshKeyboardProvider" => RefreshKeyboardProvider(),
                "SetFanLevel" => SetFanLevel(request.Value),
                "SetFanPercent" => SetFanPercent(request.Value),
                "ReturnFanToAuto" => ReturnFanToAuto(),
                "SetCoolingProfile" => SetCoolingProfile(request.Value),
                "SetCoolingCurve" => SetCoolingCurve(request.Value),
                "SetCustomCoolingCurve" => SetCustomCoolingCurve(request.Value),
                "StartFanCharacterization" => StartFanCharacterization(),
                "MarkFanLevelAudible" => MarkFanLevelAudible(),
                "StopFanCharacterization" => StopFanCharacterization(),
                "SetKeyboardBacklight" => SetKeyboardBacklight(request.Value),
                "SetThermalMode" => SetThermalMode(request.Value),
                "SetBatteryChargeLimit" => SetBatteryChargeLimit(request.Value),
                _ => Error("Unsupported operation. Raw EC, port and IOCTL passthrough are never exposed by ThinkControl.")
            };
        }
        catch (Exception ex) { return Error($"Hardware operation failed safely: {ex.Message}"); }
    }

    private ServiceResponse GetCachedStatusAndRequestRefresh()
    {
        SignalStatusDemand();
        lock (_statusGate) return _lastStatus ?? ProviderDiscoveryResponse("Service online · detecting hardware providers");
    }

    private static bool ThinkControlOwnsFan(CoolingSupervisorSnapshot cooling) =>
        cooling.AppliedLevel.HasValue ||
        cooling.AppliedPercent.HasValue ||
        !string.IsNullOrWhiteSpace(cooling.ProfileId) ||
        !cooling.Profile.Equals("Lenovo Auto", StringComparison.OrdinalIgnoreCase);

    private ServiceResponse RefreshProviders()
    {
        CoolingSupervisorSnapshot cooling = _fanSupervisor.Snapshot();
        if (cooling.Characterization.Running)
            return Error("Stop fan characterization before refreshing hardware providers.");

        if (ThinkControlOwnsFan(cooling) && !_fanSupervisor.ReturnToAuto(out string? handoffError))
            return Error("Provider refresh was blocked because ThinkControl could not safely return fan ownership to Lenovo Auto. " +
                         (handoffError ?? "Retry Lenovo Auto, then refresh providers again."));

        _hardware.RefreshProviders();
        ResetBatteryProtectionCache();
        ServiceResponse discovering = ProviderDiscoveryResponse("Providers recycled · re-detecting PawnIO/LHM, X9 EC and Lenovo keyboard backends");
        lock (_statusGate) _lastStatus = discovering;
        SignalStatusDemand();
        return discovering;
    }

    private ServiceResponse RefreshSensorProviders()
    {
        CoolingSupervisorSnapshot cooling = _fanSupervisor.Snapshot();
        if (cooling.Characterization.Running)
            return Error("Stop fan characterization before refreshing sensor providers.");

        if (ThinkControlOwnsFan(cooling) && !_fanSupervisor.ReturnToAuto(out string? handoffError))
            return Error("Sensor refresh was blocked because ThinkControl could not safely return fan ownership to Lenovo Auto. " +
                         (handoffError ?? "Retry Lenovo Auto, then refresh sensors again."));

        _hardware.RefreshSensorProviders();
        ServiceResponse status = RefreshAndReturnStatus();
        ServiceLog.Write("Sensor providers refreshed without recycling the verified EC or keyboard backend.");
        return status;
    }

    private ServiceResponse RefreshKeyboardProvider()
    {
        _hardware.RefreshKeyboardProvider();
        ServiceResponse status = RefreshAndReturnStatus();
        ServiceLog.Write("Lenovo keyboard provider refreshed independently.");
        return status;
    }

    private ServiceResponse BuildStatusResponse()
    {
        LenovoHardwareStatus status = _hardware.ReadStatus();
        CoolingSupervisorSnapshot cooling = _fanSupervisor.Snapshot();
        LenovoCoolingPolicySnapshot firmwareCooling = _coolingPolicy.Snapshot();
        LenovoBatteryChargeProtectionStatus batteryProtection = ReadBatteryChargeProtection();
        bool firmwareOverride = firmwareCooling.OverrideActive;
        FanTelemetrySnapshot[] fans = status.Fans.Select((fan, index) =>
            new FanTelemetrySnapshot(fan.Id, fan.Label, fan.Rpm, fan.Source, index == 0)).ToArray();
        HardwareSensorSnapshot[] sensors = status.Sensors.Select(sensor =>
            new HardwareSensorSnapshot(sensor.Id, sensor.HardwareName, sensor.HardwareType, sensor.Name,
                sensor.SensorType, sensor.Value, sensor.Unit, sensor.ControlTemperature, sensor.Source)).ToArray();

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
            CoolingProfile: firmwareOverride ? firmwareCooling.Profile : cooling.Profile,
            CoolingAppliedLevel: firmwareOverride ? null : cooling.AppliedLevel,
            CoolingSmoothedTemperatureC: firmwareOverride ? null : cooling.SmoothedTemperatureC,
            CoolingStatus: firmwareOverride ? firmwareCooling.Status : cooling.Status,
            CoolingSafetyOverride: firmwareOverride ? false : cooling.SafetyOverride,
            FanCharacterization: cooling.Characterization,
            CoolingProfileId: firmwareOverride ? firmwareCooling.ProfileId : cooling.ProfileId,
            CoolingAppliedPercent: firmwareOverride ? null : cooling.AppliedPercent,
            KeyboardBackend: status.KeyboardBackend,
            BatteryChargeLimitPercent: batteryProtection.Available ? batteryProtection.LimitPercent : null,
            BatteryChargeProtectionSource: batteryProtection.Available ? "Lenovo WMI · Long-Life charge type" : null,
            BatteryChargeProtectionDetail: batteryProtection.Detail);

        bool firmwareProfileControl = firmwareCooling.Supported;
        bool productFanControl = status.CanFanControl || firmwareProfileControl;
        string fanControlKind = status.CanFanControl
            ? ToFanControlKind(status.FanControlKind)
            : firmwareProfileControl
                ? FanControlKinds.FirmwarePolicy
                : FanControlKinds.None;
        bool fanCalibrationSupported = status.CanFanControl &&
                                       status.CanFanTelemetry &&
                                       string.Equals(fanControlKind, FanControlKinds.DiscreteEc, StringComparison.Ordinal);
        bool completeCalibration = cooling.Characterization.TotalLevels > 0 &&
                                   cooling.Characterization.Levels.Count == cooling.Characterization.TotalLevels &&
                                   cooling.Characterization.Levels.All(static level => level.Stable);
        bool fanCalibrationRequired = fanCalibrationSupported &&
                                      (cooling.Characterization.Running || !completeCalibration);

        // Repeated user-session effects require a provider whose direct/static write
        // contract can be called rapidly without invoking an OEM popup. Keep this
        // provider-specific decision on the service side; generic UI consumes only
        // the capability bit and never infers support from a vendor/backend label.
        bool keyboardEffects = status.CanKeyboardBacklight &&
                               !status.KeyboardBackend.Contains("Vantage", StringComparison.OrdinalIgnoreCase) &&
                               !status.KeyboardBackend.Equals("Not exposed", StringComparison.OrdinalIgnoreCase);

        var capabilities = new HardwareCapabilitySnapshot(
            status.CanFanTelemetry,
            productFanControl,
            status.CanKeyboardBacklight,
            status.CanCpuTemperature,
            status.CanSensorTelemetry,
            fans.Length,
            fanControlKind,
            FanCalibrationSupported: fanCalibrationSupported,
            FanCalibrationRequired: fanCalibrationRequired,
            KeyboardEffects: keyboardEffects,
            BatteryChargeProtection: batteryProtection.Available && batteryProtection.Writable);
        return new ServiceResponse(ThinkControlProtocol.Version, true, Telemetry: telemetry, Capabilities: capabilities);
    }

    private LenovoBatteryChargeProtectionStatus ReadBatteryChargeProtection(bool force = false)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        lock (_batteryProtectionGate)
        {
            if (!force && _batteryProtectionStatus is not null && now < _batteryProtectionRefreshAfter)
                return _batteryProtectionStatus;
        }

        LenovoBatteryChargeProtectionStatus current = LenovoBatteryChargeProtectionService.Read(_hardware.Identity);
        lock (_batteryProtectionGate)
        {
            _batteryProtectionStatus = current;
            _batteryProtectionRefreshAfter = now + BatteryProtectionProbeInterval;
        }
        return current;
    }

    private void ResetBatteryProtectionCache()
    {
        lock (_batteryProtectionGate)
        {
            _batteryProtectionStatus = null;
            _batteryProtectionRefreshAfter = DateTimeOffset.MinValue;
        }
    }

    private static string ToFanControlKind(LenovoFanControlKind kind) => kind switch
    {
        LenovoFanControlKind.LenovoOtherModeTargetRpm => FanControlKinds.OemTargetRpm,
        LenovoFanControlKind.ThinkPadEcDiscrete => FanControlKinds.DiscreteEc,
        _ => FanControlKinds.None
    };

    private ServiceResponse RefreshAndReturnStatus()
    {
        ServiceResponse status = BuildStatusResponse();
        lock (_statusGate) _lastStatus = status;
        return status;
    }

    private static ServiceResponse ProviderDiscoveryResponse(string detail)
    {
        var telemetry = new TelemetrySnapshot(
            null, "Detecting", null, "Detecting",
            "Lenovo managed · provider discovery in progress",
            detail,
            "Detecting…",
            Fans: Array.Empty<FanTelemetrySnapshot>(),
            Sensors: Array.Empty<HardwareSensorSnapshot>(),
            CoolingStatus: "Lenovo firmware owns fan control while providers are detected");
        return new ServiceResponse(
            ThinkControlProtocol.Version,
            true,
            Telemetry: telemetry,
            Capabilities: new HardwareCapabilitySnapshot(false, false, false, false, false, 0, FanControlKinds.None));
    }

    private ServiceResponse SetFanLevel(string? raw)
    {
        if (!int.TryParse(raw, out int level)) return Error("Fan level is missing or invalid.");
        return _fanSupervisor.SetManualLevel(level, out string? error) ? RefreshAndReturnStatus() : Error(error ?? "Fan level rejected.");
    }

    private ServiceResponse SetFanPercent(string? raw)
    {
        if (!int.TryParse(raw, out int percent)) return Error("Fan percentage is missing or invalid.");
        return _fanSupervisor.SetManualPercent(percent, out string? error) ? RefreshAndReturnStatus() : Error(error ?? "Fan percentage rejected.");
    }

    private ServiceResponse ReturnFanToAuto()
    {
        if (!_fanSupervisor.ReturnToAuto(out string? fanError))
            return Error(fanError ?? "Lenovo Auto rejected.");
        // Explicit Auto is also the recovery path after a service/app restart lost
        // in-memory ownership of an alpha.41 full-speed override. The coordinator
        // touches only the exact known boolean feature and verifies the release.
        if (!_coolingPolicy.RequestFirmwareAuto(out string? policyError))
            return Error(policyError ?? "Lenovo firmware cooling profile could not return to Auto.");
        return RefreshAndReturnStatus();
    }

    private ServiceResponse SetCoolingProfile(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Error("Fan profile is missing.");
        string normalized = value.Trim();
        if (normalized.Equals("Lenovo Auto", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            return ReturnFanToAuto();
        }

        LenovoHardwareStatus status = _hardware.ReadStatus();
        if (_coolingPolicy.Supported && !status.CanFanControl && LenovoCoolingPolicyCoordinator.IsBuiltInProfile(normalized))
        {
            // Release any stale ThinkControl-owned direct target first. The profile
            // itself is then owned and smoothed by Lenovo firmware/LITSSvc instead of
            // the physically rejected target-RPM writer or the inferior EC fallback.
            if (!_fanSupervisor.ReturnToAuto(out string? handoffError))
                return Error(handoffError ?? "Could not return direct fan ownership to Lenovo Auto before applying the firmware profile.");
            return _coolingPolicy.SetBuiltInProfile(normalized, out string? policyError)
                ? RefreshAndReturnStatus()
                : Error(policyError ?? "Lenovo firmware cooling profile rejected the request.");
        }

        if (_coolingPolicy.Snapshot().OverrideActive && !_coolingPolicy.ClearProfileOverride(out string? clearError))
            return Error(clearError ?? "The active Lenovo firmware cooling override could not be cleared before direct fan control.");

        return _fanSupervisor.SetProfile(normalized, out string? error)
            ? RefreshAndReturnStatus()
            : Error(error ?? "Fan profile rejected.");
    }

    private ServiceResponse SetCoolingCurve(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Error("Fan curve is missing.");

        FanCurveDefinition? definition;
        try { definition = JsonSerializer.Deserialize<FanCurveDefinition>(value, JsonOptions); }
        catch (JsonException) { return Error("Fan curve is malformed."); }
        if (definition is null)
            return Error("Fan curve is missing.");

        LenovoHardwareStatus status = _hardware.ReadStatus();
        if (_coolingPolicy.Supported && !status.CanFanControl && LenovoCoolingPolicyCoordinator.IsBuiltInProfile(definition.Id))
            return SetCoolingProfile(definition.Name);
        if (_coolingPolicy.Supported && !status.CanFanControl)
            return Error("Custom fan curves require a physically accepted direct fan writer. Built-in Quiet, Balanced and Max cooling remain available through Lenovo firmware policy.");

        if (_coolingPolicy.Snapshot().OverrideActive && !_coolingPolicy.ClearProfileOverride(out string? clearError))
            return Error(clearError ?? "The active Lenovo firmware cooling override could not be cleared before applying a direct curve.");

        return _fanSupervisor.SetCurve(definition, out string? error)
            ? RefreshAndReturnStatus()
            : Error(error ?? "Fan curve rejected.");
    }

    private ServiceResponse SetCustomCoolingCurve(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Error("Custom cooling curve is missing.");

        LenovoHardwareStatus status = _hardware.ReadStatus();
        if (_coolingPolicy.Supported && !status.CanFanControl)
            return Error("Custom fan curves require a physically accepted direct fan writer. Built-in firmware cooling profiles remain available.");

        double[]? thresholds;
        try { thresholds = JsonSerializer.Deserialize<double[]>(value, JsonOptions); }
        catch (JsonException) { return Error("Custom cooling curve is malformed."); }

        if (_coolingPolicy.Snapshot().OverrideActive && !_coolingPolicy.ClearProfileOverride(out string? clearError))
            return Error(clearError ?? "The active Lenovo firmware cooling override could not be cleared before applying a custom curve.");

        return _fanSupervisor.SetCustomCurve(thresholds, out string? error)
            ? RefreshAndReturnStatus()
            : Error(error ?? "Custom cooling curve rejected.");
    }

    private ServiceResponse StartFanCharacterization() =>
        _fanSupervisor.StartCharacterization(out string? error) ? RefreshAndReturnStatus() : Error(error ?? "Fan characterization could not start.");

    private ServiceResponse MarkFanLevelAudible() =>
        _fanSupervisor.MarkCurrentLevelAudible(out string? error) ? RefreshAndReturnStatus() : Error(error ?? "Audible fan state could not be recorded.");

    private ServiceResponse StopFanCharacterization() =>
        _fanSupervisor.StopCharacterization(out string? error) ? RefreshAndReturnStatus() : Error(error ?? "Fan characterization could not stop.");

    private ServiceResponse SetKeyboardBacklight(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Error("Keyboard level is missing.");
        return _hardware.SetKeyboardBacklight(value, out string? error) ? RefreshAndReturnStatus() : Error(error ?? "Keyboard level rejected.");
    }

    private ServiceResponse SetThermalMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Error("Thermal mode is missing.");
        return _coolingPolicy.SetBasePowerMode(value, out string? detail)
            ? new ServiceResponse(ThinkControlProtocol.Version, true, detail)
            : Error(detail ?? "Lenovo thermal policy rejected the request.");
    }

    private ServiceResponse SetBatteryChargeLimit(string? value)
    {
        if (!int.TryParse(value, out int percent) || percent is not 80 and not 100)
            return Error("Battery charge protection supports 80% Battery care or 100% Full charge only on this provider.");

        if (!LenovoBatteryChargeProtectionService.TrySetLimit(_hardware.Identity, percent, out _, out string? detail))
            return Error(detail ?? "Battery charge protection rejected the request.");

        ResetBatteryProtectionCache();
        ServiceLog.Write($"Battery charge limit changed to {percent}% through verified Lenovo charge-type semantics.");
        return RefreshAndReturnStatus();
    }

    private static ServiceResponse Error(string message) => new(ThinkControlProtocol.Version, false, message);

    private static async Task WriteAsync(NamedPipeServerStream pipe, ServiceResponse response, CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response, JsonOptions) + "\n");
        await pipe.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _disposeCts.Cancel();
        try { _statusWake.Release(); } catch { }
        try { _statusRefreshTask?.Wait(TimeSpan.FromSeconds(1)); } catch { }
        try { _coolingPolicy.ClearProfileOverride(out _); } catch { }
        _fanSupervisor.Dispose();
        _hardware.Dispose();
        _statusWake.Dispose();
        _disposeCts.Dispose();
    }
}