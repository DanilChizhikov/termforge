namespace DTech.Core.Editor.Sudo;

public sealed record FileAccessRequest(Guid ConnectionId, string? Path, int OwnerId);
