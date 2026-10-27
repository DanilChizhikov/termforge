using System.Globalization;
using System.Text.RegularExpressions;

namespace DTech.Core.Updates;

public sealed partial class SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
{
    private readonly string[] _preReleaseIdentifiers;

    private SemanticVersion(int major, int minor, int patch, string[] preReleaseIdentifiers)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        _preReleaseIdentifiers = preReleaseIdentifiers;
    }

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    public bool IsPreRelease => _preReleaseIdentifiers.Length > 0;

    public static bool TryParse(string? text, out SemanticVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = Pattern().Match(text.Trim());
        if (!match.Success)
        {
            return false;
        }

        var styles = NumberStyles.None;
        var culture = CultureInfo.InvariantCulture;
        var majorParsed = int.TryParse(match.Groups["major"].Value, styles, culture, out var major);
        var minorParsed = int.TryParse(match.Groups["minor"].Value, styles, culture, out var minor);
        var patchParsed = int.TryParse(match.Groups["patch"].Value, styles, culture, out var patch);
        if (!majorParsed || !minorParsed || !patchParsed)
        {
            return false;
        }

        var preRelease = match.Groups["pre"].Value;
        var identifiers = preRelease.Length == 0 ? [] : preRelease.Split('.');
        version = new SemanticVersion(major, minor, patch, identifiers);
        return true;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var core = CompareCore(other);
        if (core != 0)
        {
            return core;
        }

        return ComparePreRelease(other);
    }

    public bool Equals(SemanticVersion? other)
    {
        return other is not null && CompareTo(other) == 0;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as SemanticVersion);
    }

    public override int GetHashCode()
    {
        var preRelease = string.Join('.', _preReleaseIdentifiers);
        return HashCode.Combine(Major, Minor, Patch, preRelease);
    }

    public override string ToString()
    {
        var core = $"{Major}.{Minor}.{Patch}";
        return IsPreRelease ? $"{core}-{string.Join('.', _preReleaseIdentifiers)}" : core;
    }

    private int CompareCore(SemanticVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0)
        {
            return major;
        }

        var minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    private int ComparePreRelease(SemanticVersion other)
    {
        if (!IsPreRelease && !other.IsPreRelease)
        {
            return 0;
        }

        if (!IsPreRelease)
        {
            return 1;
        }

        if (!other.IsPreRelease)
        {
            return -1;
        }

        var shared = Math.Min(_preReleaseIdentifiers.Length, other._preReleaseIdentifiers.Length);
        for (var i = 0; i < shared; i++)
        {
            var result = CompareIdentifier(_preReleaseIdentifiers[i], other._preReleaseIdentifiers[i]);
            if (result != 0)
            {
                return result;
            }
        }

        return _preReleaseIdentifiers.Length.CompareTo(other._preReleaseIdentifiers.Length);
    }

    private static int CompareIdentifier(string left, string right)
    {
        var leftNumeric = IsNumeric(left);
        var rightNumeric = IsNumeric(right);
        if (leftNumeric && rightNumeric)
        {
            var leftDigits = left.TrimStart('0');
            var rightDigits = right.TrimStart('0');
            var length = leftDigits.Length.CompareTo(rightDigits.Length);
            return length != 0 ? length : string.CompareOrdinal(leftDigits, rightDigits);
        }

        if (leftNumeric)
        {
            return -1;
        }

        if (rightNumeric)
        {
            return 1;
        }

        return string.CompareOrdinal(left, right);
    }

    private static bool IsNumeric(string identifier)
    {
        for (var i = 0; i < identifier.Length; i++)
        {
            if (!char.IsAsciiDigit(identifier[i]))
            {
                return false;
            }
        }

        return identifier.Length > 0;
    }

    [GeneratedRegex(@"^[vV]?(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:-(?<pre>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$")]
    private static partial Regex Pattern();
}
