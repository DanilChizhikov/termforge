namespace DTech.Entrypoint.Editor;

internal sealed class EditorOptions
{
    public const string SectionName = "Editor";

    public long MaxFileBytes { get; set; } = 2 * 1024 * 1024;

    public int MaxBackupsPerFile { get; set; } = 10;
}
