namespace DTech.Core.Terminal.Recording;

public sealed record TerminalRecordingSettings(bool Enabled, int MaxRecordingSizeMegabytes, int MaxRecordings, int RetentionDays)
{
    private const long BytesPerMegabyte = 1024 * 1024;

    public static TerminalRecordingSettings Default { get; } = new(false, 10, 50, 30);

    public long MaxRecordingBytes => MaxRecordingSizeMegabytes * BytesPerMegabyte;

    public TimeSpan RetentionPeriod => TimeSpan.FromDays(RetentionDays);
}
