using System.IO;
using System.Text.Json;

namespace ThinkControl.UI.Services;

internal sealed record DiagnosticLifecycleState(
    string LastHandledDeviceFingerprint = "",
    string LastHandledDeviceAtUtc = "",
    string LastPromptedDeviceFingerprint = "");

/// <summary>
/// Small local lifecycle state for compatibility reports. This is intentionally
/// separate from user preferences: it answers "have these exact findings already
/// been handled?", not "what does the user prefer?".
/// </summary>
internal sealed class DiagnosticLifecycleStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly object _gate = new();
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ThinkControl",
        "diagnostics-state.json");
    private DiagnosticLifecycleState _current;

    internal DiagnosticLifecycleStore()
    {
        _current = Load();
    }

    internal DiagnosticLifecycleState Current
    {
        get { lock (_gate) return _current; }
    }

    internal bool IsHandled(string fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
            return false;
        lock (_gate)
            return string.Equals(_current.LastHandledDeviceFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase);
    }

    internal bool WasPrompted(string fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
            return false;
        lock (_gate)
            return string.Equals(_current.LastPromptedDeviceFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase);
    }

    internal void MarkPrompted(string fingerprint) => Update(state => state with
    {
        LastPromptedDeviceFingerprint = NormalizeFingerprint(fingerprint)
    });

    internal void MarkHandled(string fingerprint) => Update(state => state with
    {
        LastHandledDeviceFingerprint = NormalizeFingerprint(fingerprint),
        LastHandledDeviceAtUtc = DateTimeOffset.UtcNow.ToString("O"),
        LastPromptedDeviceFingerprint = NormalizeFingerprint(fingerprint)
    });

    internal void Clear()
    {
        lock (_gate)
        {
            _current = new DiagnosticLifecycleState();
            try { File.Delete(_path); } catch { }
        }
    }

    private void Update(Func<DiagnosticLifecycleState, DiagnosticLifecycleState> mutation)
    {
        lock (_gate)
        {
            _current = Sanitize(mutation(_current));
            Save(_current);
        }
    }

    private DiagnosticLifecycleState Load()
    {
        try
        {
            if (!File.Exists(_path))
                return new DiagnosticLifecycleState();
            DiagnosticLifecycleState? parsed = JsonSerializer.Deserialize<DiagnosticLifecycleState>(File.ReadAllText(_path), JsonOptions);
            return parsed is null ? new DiagnosticLifecycleState() : Sanitize(parsed);
        }
        catch
        {
            return new DiagnosticLifecycleState();
        }
    }

    private void Save(DiagnosticLifecycleState state)
    {
        try
        {
            string? folder = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);
            string temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(state, JsonOptions));
            File.Move(temporary, _path, overwrite: true);
        }
        catch { }
    }

    private static DiagnosticLifecycleState Sanitize(DiagnosticLifecycleState state) => state with
    {
        LastHandledDeviceFingerprint = NormalizeFingerprint(state.LastHandledDeviceFingerprint),
        LastPromptedDeviceFingerprint = NormalizeFingerprint(state.LastPromptedDeviceFingerprint),
        LastHandledDeviceAtUtc = DateTimeOffset.TryParse(state.LastHandledDeviceAtUtc, out DateTimeOffset handled)
            ? handled.ToUniversalTime().ToString("O")
            : string.Empty
    };

    private static string NormalizeFingerprint(string? value)
    {
        string safe = new((value ?? string.Empty)
            .Where(ch => char.IsAsciiHexDigit(ch))
            .Take(128)
            .ToArray());
        return safe.ToUpperInvariant();
    }
}
