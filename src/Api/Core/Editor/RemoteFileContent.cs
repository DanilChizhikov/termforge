namespace DTech.Core.Editor;

public readonly record struct RemoteFileContent(string Path, ReadOnlyMemory<byte> Content);
