using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace ThinkControl.UI.Services;

public sealed record UpdateCheckResult(
    bool Available,
    string Status,
    string? Version = null,
    string? Url = null,
    string? InstallerUrl = null,
    string? PayloadUrl = null,
    string? ChecksumUrl = null);

public sealed record UpdateInstallResult(
    bool Success,
    string Status,
    string? InstallerPath = null,
    string? PayloadPath = null);

public sealed class UpdateService
{
    private const string ReleasesEndpoint = "https://api.github.com/repos/Hugowhitee/ThinkControl/releases?per_page=10";
    private const string ReleasesPage = "https://github.com/Hugowhitee/ThinkControl/releases";
    private const string TrustedDownloadPrefix = "https://github.com/Hugowhitee/ThinkControl/releases/download/";
    private readonly HttpClient _httpClient;

    public UpdateService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
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

            return assembly.GetName().Version?.ToString(3) ?? "0.1.0";
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

                (string? installerUrl, string? payloadUrl, string? checksumUrl) = FindReleaseAssets(release);
                bool ready = installerUrl is not null && payloadUrl is not null && checksumUrl is not null;
                bool available = latest.CompareTo(current) > 0;
                string status = available
                    ? ready
                        ? $"{tag} is ready to install"
                        : $"{tag} is available · release assets are still publishing"
                    : $"Up to date · {tag}";

                return new(
                    available,
                    status,
                    tag,
                    string.IsNullOrWhiteSpace(url) ? ReleasesPage : url,
                    installerUrl,
                    payloadUrl,
                    checksumUrl);
            }

            return new(false, "No compatible public release yet", Url: ReleasesPage);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or FormatException)
        {
            return new(false, "Could not reach the release channel", Url: ReleasesPage);
        }
    }

    public Task<UpdateInstallResult> DownloadAndLaunchAsync(
        UpdateCheckResult update,
        CancellationToken cancellationToken = default) =>
        DownloadAndLaunchAsync(update, progress: null, cancellationToken);

    public async Task<UpdateInstallResult> DownloadAndLaunchAsync(
        UpdateCheckResult update,
        IProgress<string>? progress,
        CancellationToken cancellationToken = default)
    {
        if (!update.Available)
            return new(false, "No newer ThinkControl release is available.");
        if (!IsTrustedReleaseUrl(update.InstallerUrl) ||
            !IsTrustedReleaseUrl(update.PayloadUrl) ||
            !IsTrustedReleaseUrl(update.ChecksumUrl))
        {
            return new(false, "The release is missing verified ThinkControl update assets.");
        }

        try
        {
            string installerName = GetTrustedAssetName(update.InstallerUrl!, "ThinkControl-Setup-", ".exe");
            string payloadName = GetTrustedAssetName(update.PayloadUrl!, "ThinkControl-Payload-", ".zip");

            progress?.Report($"Downloading {update.Version ?? "update"}…");
            Task<byte[]> installerTask = _httpClient.GetByteArrayAsync(update.InstallerUrl!, cancellationToken);
            Task<byte[]> payloadTask = _httpClient.GetByteArrayAsync(update.PayloadUrl!, cancellationToken);
            Task<string> sumsTask = _httpClient.GetStringAsync(update.ChecksumUrl!, cancellationToken);
            await Task.WhenAll(installerTask, payloadTask, sumsTask).ConfigureAwait(false);

            byte[] installerBytes = await installerTask.ConfigureAwait(false);
            byte[] payloadBytes = await payloadTask.ConfigureAwait(false);
            string sums = await sumsTask.ConfigureAwait(false);

            progress?.Report("Verifying downloaded update files…");
            if (!MatchesPublishedHash(installerBytes, installerName, sums))
                return new(false, "The downloaded installer failed SHA-256 verification and was not started.");
            if (!MatchesPublishedHash(payloadBytes, payloadName, sums))
                return new(false, "The downloaded application payload failed SHA-256 verification and was not started.");

            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ThinkControl",
                "updates");
            Directory.CreateDirectory(folder);
            CleanupOldUpdateFiles(folder, installerName, payloadName);

            string installerPath = Path.Combine(folder, installerName);
            string payloadPath = Path.Combine(folder, payloadName);
            await File.WriteAllBytesAsync(installerPath, installerBytes, cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(payloadPath, payloadBytes, cancellationToken).ConfigureAwait(false);

            string logPath = Path.Combine(folder, "last-update.log");
            var start = new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = folder
            };
            start.ArgumentList.Add("/VERYSILENT");
            start.ArgumentList.Add("/SUPPRESSMSGBOXES");
            start.ArgumentList.Add("/NORESTART");
            start.ArgumentList.Add("/CLOSEAPPLICATIONS");
            start.ArgumentList.Add("/UPDATE=1");
            start.ArgumentList.Add("/RELAUNCH=1");
            start.ArgumentList.Add($"/PAYLOAD={payloadPath}");
            start.ArgumentList.Add($"/LOG={logPath}");

            // Keep ThinkControl visible until the user has explicitly approved the
            // Windows elevation prompt. The installer itself closes the running UI
            // only after its already-downloaded local payload is ready to replace.
            progress?.Report("Ready to install · approve the Windows administrator prompt");
            Process? process = Process.Start(start);
            if (process is null)
                return new(false, "Windows could not start the verified updater.");

            return new(
                true,
                $"Installer started for {update.Version ?? "the update"} · ThinkControl will close automatically when installation begins",
                installerPath,
                payloadPath);
        }
        catch (InvalidDataException ex)
        {
            return new(false, ex.Message);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new(false, "Update installation was cancelled. ThinkControl will not ask again unless you choose Install update.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or CryptographicException)
        {
            return new(false, $"Update could not be installed automatically: {ex.Message}");
        }
    }

    public static void OpenRelease(UpdateCheckResult result)
    {
        string target = string.IsNullOrWhiteSpace(result.Url) ? ReleasesPage : result.Url;
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    }

    private static (string? Installer, string? Payload, string? Checksums) FindReleaseAssets(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out JsonElement assets) || assets.ValueKind != JsonValueKind.Array)
            return (null, null, null);

        string? installer = null;
        string? payload = null;
        string? checksums = null;
        foreach (JsonElement asset in assets.EnumerateArray())
        {
            string? name = asset.TryGetProperty("name", out JsonElement nameElement) ? nameElement.GetString() : null;
            string? url = asset.TryGetProperty("browser_download_url", out JsonElement urlElement) ? urlElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
                continue;

            if (name.StartsWith("ThinkControl-Setup-", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                installer = url;
            else if (name.StartsWith("ThinkControl-Payload-", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                payload = url;
            else if (name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))
                checksums = url;
        }

        return (installer, payload, checksums);
    }

    private static string GetTrustedAssetName(string url, string prefix, string suffix)
    {
        Uri uri = new(url);
        string name = Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath));
        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The release contains an unrecognized ThinkControl update asset.");
        }
        return name;
    }

    private static bool MatchesPublishedHash(byte[] bytes, string fileName, string sums)
    {
        string? expectedHash = FindExpectedHash(sums, fileName);
        if (expectedHash is null)
            return false;
        string actualHash = Convert.ToHexString(SHA256.HashData(bytes));
        return actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindExpectedHash(string sums, string fileName)
    {
        foreach (string raw in sums.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string line = raw.Trim();
            if (!line.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                continue;
            string hash = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            if (hash.Length == 64 && hash.All(Uri.IsHexDigit))
                return hash.ToUpperInvariant();
        }
        return null;
    }

    private static bool IsTrustedReleaseUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        value!.StartsWith(TrustedDownloadPrefix, StringComparison.OrdinalIgnoreCase);

    private static void CleanupOldUpdateFiles(string folder, string keepInstaller, string keepPayload)
    {
        try
        {
            foreach (string path in Directory.EnumerateFiles(folder, "ThinkControl-*"))
            {
                string name = Path.GetFileName(path);
                if (!name.Equals(keepInstaller, StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals(keepPayload, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(path); } catch { }
                }
            }
        }
        catch
        {
        }
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
