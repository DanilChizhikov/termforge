using DTech.Core.CommandHistory;

namespace DTech.Persistence.CommandHistory;

internal static class CommandHistoryMapper
{
    public static CommandHistoryEntry ToModel(CommandHistoryEntity entity)
    {
        return new CommandHistoryEntry(entity.Id, entity.ConnectionId, entity.Command, entity.ExecutedAt, entity.ExitCode, entity.IsPinned);
    }

    public static CommandHistoryEntity ToEntity(CommandHistoryEntry model)
    {
        return new CommandHistoryEntity
        {
            Id = model.Id,
            ConnectionId = model.ConnectionId,
            Command = model.Command,
            ExecutedAt = model.ExecutedAt,
            ExitCode = model.ExitCode,
            IsPinned = model.IsPinned,
        };
    }
}
