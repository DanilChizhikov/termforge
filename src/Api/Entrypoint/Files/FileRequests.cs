namespace DTech.Entrypoint.Files;

public sealed record CreateDirectoryBody(string? ParentPath, string? Name);

public sealed record RenameBody(string? Path, string? NewName);

public sealed record DeleteBody(string? Path, bool Confirm);
