namespace DTech.Core.CommandHistory;

public interface ICommandHistoryRecorder
{
    Task<bool> RecordAsync(RecordedCommand command, CancellationToken cancellationToken);
}
