namespace ThinkControl.Core.Updates;

/// <summary>
/// ThinkControl release-version ordering.
///
/// CI development artifacts use a trailing -dev.N marker (for example
/// 0.1.0-alpha.50-dev.1767). That marker is intentionally lower precedence than
/// the canonical release with the same base version so a dev tester still receives
/// alpha.50 when it is published.
/// </summary>
public sealed record ThinkControlVersion(
    int Major,
    int Minor,
    int Patch,
    IReadOnlyList<string> PreRelease,
    int? DevelopmentBuild) : IComparable<ThinkControlVersion>
{
    public bool IsPrerelease => PreRelease.Count > 0;

    public static ThinkControlVersion Parse(string raw)
    {
        string withoutBuild = raw.Split('+')[0];
        int? developmentBuild = null;

        int devIndex = withoutBuild.LastIndexOf("-dev.", StringComparison.OrdinalIgnoreCase);
        if (devIndex > 0 &&
            int.TryParse(withoutBuild[(devIndex + 5)..], out int parsedDevelopmentBuild))
        {
            developmentBuild = parsedDevelopmentBuild;
            withoutBuild = withoutBuild[..devIndex];
        }

        string[] versionAndPre = withoutBuild.Split('-', 2);
        string[] core = versionAndPre[0].Split('.');
        if (core.Length < 3 ||
            !int.TryParse(core[0], out int major) ||
            !int.TryParse(core[1], out int minor) ||
            !int.TryParse(core[2], out int patch))
        {
            throw new FormatException($"Invalid ThinkControl version '{raw}'.");
        }

        IReadOnlyList<string> pre = versionAndPre.Length == 2
            ? versionAndPre[1].Split('.', StringSplitOptions.RemoveEmptyEntries)
            : Array.Empty<string>();

        return new ThinkControlVersion(major, minor, patch, pre, developmentBuild);
    }

    public int CompareTo(ThinkControlVersion? other)
    {
        if (other is null)
            return 1;

        int core = Major.CompareTo(other.Major);
        if (core == 0) core = Minor.CompareTo(other.Minor);
        if (core == 0) core = Patch.CompareTo(other.Patch);
        if (core != 0) return core;

        if (PreRelease.Count == 0 && other.PreRelease.Count == 0)
            return CompareDevelopment(other);
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
            if (part != 0)
                return part;
        }

        return CompareDevelopment(other);
    }

    private int CompareDevelopment(ThinkControlVersion other)
    {
        if (DevelopmentBuild.HasValue != other.DevelopmentBuild.HasValue)
            return DevelopmentBuild.HasValue ? -1 : 1;

        if (DevelopmentBuild is int leftDev && other.DevelopmentBuild is int rightDev)
            return leftDev.CompareTo(rightDev);

        return 0;
    }
}
