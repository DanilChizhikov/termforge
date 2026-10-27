namespace DTech.Core.SettingsTransfer.Document;

public sealed record ExportedPreferences
{
    public ExportedRecordingLimits? TerminalRecording { get; init; }

    public IReadOnlyList<string>? CommandExclusionPatterns { get; init; }
}
