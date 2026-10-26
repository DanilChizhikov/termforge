namespace DTech.Core.Terminal.Recording;

public static class TerminalRecordingSettingsValidator
{
    public static TerminalRecordingError? Validate(TerminalRecordingSettings settings)
    {
        if (settings.MaxRecordingSizeMegabytes is < TerminalRecordingLimits.MinRecordingSizeMegabytes or > TerminalRecordingLimits.MaxRecordingSizeMegabytes)
        {
            return TerminalRecordingError.InvalidSettings($"Maximum recording size must be between {TerminalRecordingLimits.MinRecordingSizeMegabytes} and {TerminalRecordingLimits.MaxRecordingSizeMegabytes} MB.");
        }

        if (settings.MaxRecordings is < TerminalRecordingLimits.MinRecordings or > TerminalRecordingLimits.MaxRecordings)
        {
            return TerminalRecordingError.InvalidSettings($"The number of kept recordings must be between {TerminalRecordingLimits.MinRecordings} and {TerminalRecordingLimits.MaxRecordings}.");
        }

        if (settings.RetentionDays is < TerminalRecordingLimits.MinRetentionDays or > TerminalRecordingLimits.MaxRetentionDays)
        {
            return TerminalRecordingError.InvalidSettings($"Recordings must be kept between {TerminalRecordingLimits.MinRetentionDays} and {TerminalRecordingLimits.MaxRetentionDays} days.");
        }

        return null;
    }
}
