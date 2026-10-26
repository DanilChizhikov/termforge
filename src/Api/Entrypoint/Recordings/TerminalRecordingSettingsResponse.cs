using DTech.Core.Terminal.Recording;

namespace DTech.Entrypoint.Recordings;

internal sealed record TerminalRecordingSettingsResponse(bool Enabled, int MaxRecordingSizeMegabytes, int MaxRecordings, int RetentionDays)
{
    public required string Directory { get; init; }

    public static TerminalRecordingSettingsResponse From(TerminalRecordingSettings settings, TerminalRecordingStorage storage)
    {
        return new TerminalRecordingSettingsResponse(settings.Enabled, settings.MaxRecordingSizeMegabytes, settings.MaxRecordings, settings.RetentionDays)
        {
            Directory = storage.DirectoryPath,
        };
    }
}
