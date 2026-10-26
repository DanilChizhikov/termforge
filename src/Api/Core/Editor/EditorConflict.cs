namespace DTech.Core.Editor;

public sealed record EditorConflict(RemoteFileVersion? CurrentVersion, TextContent? CurrentContent, string Reason);
