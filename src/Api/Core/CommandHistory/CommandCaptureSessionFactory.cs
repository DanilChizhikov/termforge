namespace DTech.Core.CommandHistory;

public sealed class CommandCaptureSessionFactory(ICommandHistoryRecorder recorder, TimeProvider timeProvider)
{
    public CommandCaptureSession Create(Guid connectionId)
    {
        return new CommandCaptureSession(connectionId, recorder, timeProvider);
    }
}
