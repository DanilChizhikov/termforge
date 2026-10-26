using System.Globalization;

namespace DTech.Core.Editor;

public sealed class AuxiliaryFileNames
{
    private const string Marker = ".termforge-";
    private const string BackupSuffix = ".bak";
    private const string TemporarySuffix = ".tmp";
    private const string AsideSuffix = ".old";
    private const string TimestampFormat = "yyyyMMdd'T'HHmmssfff'Z'";

    public string Backup(string fileName, DateTime createdAtUtc)
    {
        var timestamp = createdAtUtc.ToString(TimestampFormat, CultureInfo.InvariantCulture);
        return $".{fileName}{Marker}{timestamp}{BackupSuffix}";
    }

    public string Temporary(string fileName)
    {
        return $".{fileName}{Marker}{Guid.NewGuid():N}{TemporarySuffix}";
    }

    public string Aside(string fileName)
    {
        return $".{fileName}{Marker}{Guid.NewGuid():N}{AsideSuffix}";
    }

    public DateTime? ParseBackup(string fileName, string candidate)
    {
        var prefix = $".{fileName}{Marker}";
        var isBackupName = candidate.Length > prefix.Length + BackupSuffix.Length
            && candidate.StartsWith(prefix, StringComparison.Ordinal)
            && candidate.EndsWith(BackupSuffix, StringComparison.Ordinal);
        if (!isBackupName)
        {
            return null;
        }

        var timestamp = candidate[prefix.Length..^BackupSuffix.Length];
        var styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
        var parsed = DateTime.TryParseExact(timestamp, TimestampFormat, CultureInfo.InvariantCulture, styles, out var createdAtUtc);
        return parsed ? createdAtUtc : null;
    }
}
