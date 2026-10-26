namespace DTech.Persistence.CommandHistory;

public sealed class CommandHistoryEntity
{
    public Guid Id { get; set; }

    public Guid ConnectionId { get; set; }

    public required string Command { get; set; }

    public DateTime ExecutedAt { get; set; }

    public int? ExitCode { get; set; }

    public bool IsPinned { get; set; }
}
