namespace DTech.Core.CommandHistory;

public sealed record CommandHistoryQuery(Guid ConnectionId, string? Search, int Limit);
