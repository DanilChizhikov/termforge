namespace DTech.Core.Terminal.Recording;

public static class TerminalRecordingLimits
{
    public const int MinRecordingSizeMegabytes = 1;
    public const int MaxRecordingSizeMegabytes = 100;
    public const int MinRecordings = 1;
    public const int MaxRecordings = 500;
    public const int MinRetentionDays = 1;
    public const int MaxRetentionDays = 365;
    public const int OutputChannelCapacity = 1024;
}
