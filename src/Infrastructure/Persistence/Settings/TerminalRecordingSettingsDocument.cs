using DTech.Core.Terminal.Recording;

namespace DTech.Persistence.Settings;

internal sealed record TerminalRecordingSettingsDocument(bool Enabled, int MaxRecordingSizeMegabytes, int MaxRecordings, int RetentionDays)
{
    public static TerminalRecordingSettingsDocument From(TerminalRecordingSettings settings)
    {
        return new TerminalRecordingSettingsDocument(settings.Enabled, settings.MaxRecordingSizeMegabytes, settings.MaxRecordings, settings.RetentionDays);
    }

    public TerminalRecordingSettings ToSettings()
    {
        return new TerminalRecordingSettings(Enabled, MaxRecordingSizeMegabytes, MaxRecordings, RetentionDays);
    }
}
