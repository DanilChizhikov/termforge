namespace DTech.Entrypoint.Recordings;

internal sealed class TerminalRecordingOptions
{
    public const string SectionName = "Recordings";
    public const string DefaultDirectoryName = "recordings";

    public string? Directory { get; set; }
}
