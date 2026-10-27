namespace DTech.Core.Editor.Sudo;

public sealed record RemoteFileAccess(int OwnerId, string? OwnerName, bool Writable);
