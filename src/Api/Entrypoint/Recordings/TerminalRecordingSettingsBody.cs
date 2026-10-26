using DTech.Core.Terminal.Recording;

namespace DTech.Entrypoint.Recordings;

internal sealed record TerminalRecordingSettingsBody(bool Enabled, int MaxRecordingSizeMegabytes, int MaxRecordings, int RetentionDays)
{
    public TerminalRecordingSettings ToSettings()
    {
        return new TerminalRecordingSettings(Enabled, MaxRecordingSizeMegabytes, MaxRecordings, RetentionDays);
    }
}
