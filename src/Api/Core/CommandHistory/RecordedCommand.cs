namespace DTech.Core.CommandHistory;

public sealed record RecordedCommand(Guid ConnectionId, string Command, DateTime ExecutedAt, int? ExitCode);
