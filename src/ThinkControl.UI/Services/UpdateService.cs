using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace ThinkControl.UI.Services;

public sealed record UpdateCheckResult(bool Available, string Status, string? Version = null, string? Url = null);

public sealed class UpdateService
{
    private const string LatestReleaseEndpoint = "https://api.github.com/repos/Hugowhitee/ThinkControl/releases/latest";
    private readonly HttpClient _httpClient;

    public UpdateService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ThinkControl", CurrentVersion));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(LatestReleaseEndpoint, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string reason = response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "Release channel is not publicly reachable yet"
                    : $"GitHub returned {(int)response.StatusCode}";
                return new(false, reason);
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            string? tag = json.RootElement.TryGetProperty("tag_name", out JsonElement tagElement)
                ? tagElement.GetString()
                : null;
            string? url = json.RootElement.TryGetProperty("html_url", out JsonElement urlElement)
                ? urlElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(tag))
                return new(false, "No release version returned");

            Version current = ParseVersion(CurrentVersion);
            Version latest = ParseVersion(tag.TrimStart('v', 'V'));
            bool available = latest > current;
            return new(available, available ? $"{tag} is available" : $"Up to date · {tag}", tag, url);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new(false, "Could not reach the release channel");
        }
    }

    public static void OpenRelease(UpdateCheckResult result)
    {
        if (string.IsNullOrWhiteSpace(result.Url))
            return;

        Process.Start(new ProcessStartInfo(result.Url) { UseShellExecute = true });
    }

    private static Version ParseVersion(string raw)
    {
        string stablePart = raw.Split('-', '+')[0];
        return Version.TryParse(stablePart, out Version? version) ? version : new Version(0, 0, 0);
    }
}
