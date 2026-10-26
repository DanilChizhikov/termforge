using DTech.Core.CommandHistory;

namespace DTech.Entrypoint.CommandHistory;

internal sealed record CommandHistoryResponse(Guid Id, Guid ConnectionId, string Command, DateTime ExecutedAt, int? ExitCode, bool IsPinned)
{
    public static CommandHistoryResponse From(CommandHistoryEntry entry)
    {
        return new CommandHistoryResponse(entry.Id, entry.ConnectionId, entry.Command, entry.ExecutedAt, entry.ExitCode, entry.IsPinned);
    }
}
