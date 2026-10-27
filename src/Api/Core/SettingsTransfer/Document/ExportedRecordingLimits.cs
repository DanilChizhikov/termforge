namespace DTech.Core.SettingsTransfer.Document;

public sealed record ExportedRecordingLimits
{
    public int MaxRecordingSizeMegabytes { get; init; }

    public int MaxRecordings { get; init; }

    public int RetentionDays { get; init; }
}
