namespace DTech.Core.Editor;

public readonly record struct EditorReadRequest(string Path, long MaxBytes);
