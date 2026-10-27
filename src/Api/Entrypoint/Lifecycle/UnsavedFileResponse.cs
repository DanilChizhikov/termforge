namespace DTech.Entrypoint.Lifecycle;

internal sealed record UnsavedFileResponse(Guid ConnectionId, string Server, string Path);
