using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using ThinkControl.Core.Ipc;

namespace ThinkControl.UI.Services;

public sealed class HardwareServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ServiceResponse?> GetStatusAsync(CancellationToken cancellationToken = default) =>
        await SendAsync(new ServiceRequest(ThinkControlProtocol.Version, "GetStatus"), cancellationToken);

    public async Task<ServiceResponse?> SetFanLevelAsync(int level, CancellationToken cancellationToken = default) =>
        await SendAsync(new ServiceRequest(ThinkControlProtocol.Version, "SetFanLevel", level.ToString()), cancellationToken);

    public async Task<ServiceResponse?> ReturnFanToAutoAsync(CancellationToken cancellationToken = default) =>
        await SendAsync(new ServiceRequest(ThinkControlProtocol.Version, "ReturnFanToAuto"), cancellationToken);

    public async Task<ServiceResponse?> SetKeyboardBacklightAsync(string value, CancellationToken cancellationToken = default) =>
        await SendAsync(new ServiceRequest(ThinkControlProtocol.Version, "SetKeyboardBacklight", value), cancellationToken);

    private static async Task<ServiceResponse?> SendAsync(ServiceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                ThinkControlProtocol.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(450));
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
