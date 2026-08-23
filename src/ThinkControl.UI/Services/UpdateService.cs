using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace ThinkControl.UI.Services;

public sealed record UpdateCheckResult(bool Available, string Status, string? Version = null, string? Url = null);

public sealed class UpdateService
{
    private const string ReleasesEndpoint = "https://api.github.com/repos/Hugowhitee/ThinkControl/releases?per_page=10";
    private const string ReleasesPage = "https://github.com/Hugowhitee/ThinkControl/releases";
    private readonly HttpClient _httpClient;

    public UpdateService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ThinkControl", SanitizeUserAgentVersion(CurrentVersion)));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public static string CurrentVersion
    {
        get
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informational))
                return informational.Split('+')[0];

            return assembly.GetName().Version?.ToString(3) ?? "0.1.0-alpha.4";
        }
    }

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(ReleasesEndpoint, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return new(false, "No public release published yet", Url: ReleasesPage);
            if (!response.IsSuccessStatusCode)
                return new(false, "Release channel is temporarily unavailable", Url: ReleasesPage);

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (json.RootElement.ValueKind != JsonValueKind.Array)
                return new(false, "Release channel returned an unexpected response", Url: ReleasesPage);

            SemanticVersion current = SemanticVersion.Parse(CurrentVersion);
            bool allowPrerelease = current.PreRelease.Count > 0;

            foreach (JsonElement release in json.RootElement.EnumerateArray())
            {
                bool draft = release.TryGetProperty("draft", out JsonElement draftElement) && draftElement.GetBoolean();
                bool prerelease = release.TryGetProperty("prerelease", out JsonElement prereleaseElement) && prereleaseElement.GetBoolean();
                if (draft || (prerelease && !allowPrerelease))
                    continue;

                string? tag = release.TryGetProperty("tag_name", out JsonElement tagElement) ? tagElement.GetString() : null;
                string? url = release.TryGetProperty("html_url", out JsonElement urlElement) ? urlElement.GetString() : null;
                if (string.IsNullOrWhiteSpace(tag))
                    continue;

                SemanticVersion latest;
                try
                {
                    latest = SemanticVersion.Parse(tag.TrimStart('v', 'V'));
                }
                catch (FormatException)
                {
                    continue;
                }

                bool available = latest.CompareTo(current) > 0;
                return new(
                    available,
                    available ? $"{tag} is available" : $"Up to date · {tag}",
                    tag,
                    string.IsNullOrWhiteSpace(url) ? ReleasesPage : url);
            }

            return new(false, "No compatible public release yet", Url: ReleasesPage);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or FormatException)
        {
            return new(false, "Could not reach the release channel", Url: ReleasesPage);
        }
    }

    public static void OpenRelease(UpdateCheckResult result)
    {
        string target = string.IsNullOrWhiteSpace(result.Url) ? ReleasesPage : result.Url;
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    }

    private static string SanitizeUserAgentVersion(string version)
    {
        string sanitized = new(version.Where(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_').ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "0.1.0" : sanitized;
    }

    private sealed record SemanticVersion(int Major, int Minor, int Patch, IReadOnlyList<string> PreRelease) : IComparable<SemanticVersion>
    {
        internal static SemanticVersion Parse(string raw)
        {
            string withoutBuild = raw.Split('+')[0];
            string[] versionAndPre = withoutBuild.Split('-', 2);
            string[] core = versionAndPre[0].Split('.');
            if (core.Length < 3 ||
                !int.TryParse(core[0], out int major) ||
                !int.TryParse(core[1], out int minor) ||
                !int.TryParse(core[2], out int patch))
            {
                throw new FormatException($"Invalid semantic version '{raw}'.");
            }

            IReadOnlyList<string> pre = versionAndPre.Length == 2
                ? versionAndPre[1].Split('.', StringSplitOptions.RemoveEmptyEntries)
                : Array.Empty<string>();
            return new SemanticVersion(major, minor, patch, pre);
        }

        public int CompareTo(SemanticVersion? other)
        {
            if (other is null) return 1;
            int core = Major.CompareTo(other.Major);
            if (core == 0) core = Minor.CompareTo(other.Minor);
            if (core == 0) core = Patch.CompareTo(other.Patch);
            if (core != 0) return core;

            if (PreRelease.Count == 0 && other.PreRelease.Count == 0) return 0;
            if (PreRelease.Count == 0) return 1;
            if (other.PreRelease.Count == 0) return -1;

            int count = Math.Max(PreRelease.Count, other.PreRelease.Count);
            for (int i = 0; i < count; i++)
            {
                if (i >= PreRelease.Count) return -1;
                if (i >= other.PreRelease.Count) return 1;

                string left = PreRelease[i];
                string right = other.PreRelease[i];
                bool leftNumeric = int.TryParse(left, out int leftNumber);
                bool rightNumeric = int.TryParse(right, out int rightNumber);

                int part = leftNumeric && rightNumeric
                    ? leftNumber.CompareTo(rightNumber)
                    : leftNumeric
                        ? -1
                        : rightNumeric
                            ? 1
                            : string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
                if (part != 0) return part;
            }

            return 0;
        }
    }
}
