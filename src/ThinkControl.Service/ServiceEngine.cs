using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using ThinkControl.Core.Ipc;
using ThinkControl.Hardware.X9;

namespace ThinkControl.Service;

internal sealed class ServiceEngine : IDisposable
{
    private const int MaxRequestBytes = 4096;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly X9HardwareController _hardware = new();
    private readonly CancellationTokenSource _disposeCts = new();
    private bool _disposed;

    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        CancellationToken token = linked.Token;

        while (!token.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = CreateServerPipe();
                await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                _ = HandleConnectionAsync(server, token);
                server = null; // Ownership transferred to HandleConnectionAsync.
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                server?.Dispose();
                break;
            }
            catch
            {
                server?.Dispose();
                try { await Task.Delay(250, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
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
            PipeAccessRights.ReadData | PipeAccessRights.WriteData | PipeAccessRights.ReadAttributes | PipeAccessRights.Synchronize,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            ThinkControlProtocol.PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.WriteThrough,
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
                "GetStatus" => StatusResponse(),
                "SetFanLevel" => SetFanLevel(request.Value),
                "ReturnFanToAuto" => ReturnFanToAuto(),
                "SetKeyboardBacklight" => SetKeyboardBacklight(request.Value),
                _ => Error("Unsupported operation. Raw EC, port and IOCTL passthrough are never exposed by ThinkControl.")
            };
        }
        catch (Exception ex)
        {
            return Error($"Hardware operation failed safely: {ex.Message}");
        }
    }

    private ServiceResponse StatusResponse()
    {
        X9HardwareStatus status = _hardware.ReadStatus();
        var telemetry = new TelemetrySnapshot(
            status.CpuTemperatureC,
            status.CpuTemperatureSource,
            status.FanRpm,
            status.FanRpmSource,
            status.FanState,
            status.HardwareAccess,
            status.KeyboardBacklight,
            ThermalSolutionVersion: null);

        var capabilities = new HardwareCapabilitySnapshot(
            status.CanFanTelemetry,
            status.CanFanControl,
            status.CanKeyboardBacklight,
            status.CanCpuTemperature);

        return new ServiceResponse(
            ThinkControlProtocol.Version,
            true,
            Telemetry: telemetry,
            Capabilities: capabilities);
    }

    private ServiceResponse SetFanLevel(string? raw)
    {
        if (!int.TryParse(raw, out int level))
            return Error("Fan level is missing or invalid.");

        return _hardware.SetFanLevel(level, out string? error)
            ? StatusResponse()
            : Error(error ?? "Fan level rejected.");
    }

    private ServiceResponse ReturnFanToAuto()
    {
        return _hardware.ReturnFanToAuto(out string? error)
            ? StatusResponse()
            : Error(error ?? "Lenovo Auto rejected.");
    }

    private ServiceResponse SetKeyboardBacklight(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Error("Keyboard level is missing.");

        return _hardware.SetKeyboardBacklight(value, out string? error)
            ? StatusResponse()
            : Error(error ?? "Keyboard level rejected.");
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
        _hardware.Dispose();
        _disposeCts.Dispose();
    }
}
