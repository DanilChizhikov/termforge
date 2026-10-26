namespace DTech.Core.Files;

public readonly record struct DeleteRequest(string Path, bool Confirmed);
