using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.IO;
using ThinkControl.UI.Services;
namespace ThinkControl.ShellSmoke;
internal static partial class Program
{
    private static async Task ValidateUpdateTransfer()
    {
        string folder = Path.Combine(Path.GetTempPath(), "ThinkControl-transfer-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            byte[] data = new byte[200000]; Random.Shared.NextBytes(data);
            string path = Path.Combine(folder, "ThinkControl-Payload-test.zip");
            string sums = Convert.ToHexString(SHA256.HashData(data)) + "  " + Path.GetFileName(path);
            using var client = new HttpClient(new TransferHandler(data));
            await UpdateService.DownloadVerifiedFileAsync(client, "https://example.test/payload", path, sums, default);
            if (!File.ReadAllBytes(path).SequenceEqual(data) || File.Exists(path + ".partial"))
                throw new Exception("Verified transfer must publish the exact bytes and remove staging.");
            try
            {
                await UpdateService.DownloadVerifiedFileAsync(client, "https://example.test/payload", path, new string('0', 64) + "  " + Path.GetFileName(path), default);
                throw new Exception("Corrupt transfer was accepted.");
            }
            catch (InvalidDataException) { }
            if (!File.ReadAllBytes(path).SequenceEqual(data) || File.Exists(path + ".partial"))
                throw new Exception("Rejected transfer must preserve the verified file and remove staging.");
            using var cancelled = new CancellationTokenSource();
            using var cancellingClient = new HttpClient(new TransferHandler(data, cancelled));
            try
            {
                await UpdateService.DownloadVerifiedFileAsync(cancellingClient, "https://example.test/payload", path, sums, cancelled.Token);
                throw new Exception("Cancelled transfer was accepted.");
            }
            catch (OperationCanceledException) { }
            if (File.Exists(path + ".partial")) throw new Exception("Cancelled transfer left a partial download.");
        }
        finally { Directory.Delete(folder, recursive: true); }
    }
    private sealed class CancellingStream(byte[] data, CancellationTokenSource? cancel) : MemoryStream(data)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int count = await base.ReadAsync(buffer, cancellationToken);
            cancel?.Cancel();
            return count;
        }
    }
    private sealed class TransferHandler(byte[] data, CancellationTokenSource? cancel = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new CancellingStream(data, cancel)) });
        }
    }
}
