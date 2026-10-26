namespace DTech.Core.CommandHistory;

public sealed record CommandHistoryEntry(Guid Id, Guid ConnectionId, string Command, DateTime ExecutedAt, int? ExitCode, bool IsPinned);
