namespace DTech.Core.Editor;

public sealed record RemoteDocument(string Path, TextContent Content, RemoteFileVersion Version, int Mode);
