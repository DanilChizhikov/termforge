namespace DTech.Core.Editor;

public readonly record struct RemoteReadRequest(string Path, long MaxBytes);
