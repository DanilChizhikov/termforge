namespace DTech.Core.Files.Remote;

public readonly record struct RemoteDeleteRequest(string Path, string? ConfirmationToken);
