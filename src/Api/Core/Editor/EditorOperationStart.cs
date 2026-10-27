namespace DTech.Core.Editor;

public readonly record struct EditorOperationStart(string Kind, string Server, string Path)
{
    public string? BackupName { get; init; }

    public bool Elevated { get; init; }
}
