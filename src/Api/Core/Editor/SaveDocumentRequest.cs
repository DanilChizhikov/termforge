namespace DTech.Core.Editor;

public readonly record struct SaveDocumentRequest(string? Path, TextContent Content, RemoteFileVersion? ExpectedVersion);
