namespace DTech.Core.Editor;

public readonly record struct RemoteFileCreation(string Path, ReadOnlyMemory<byte> Content, int Mode);
