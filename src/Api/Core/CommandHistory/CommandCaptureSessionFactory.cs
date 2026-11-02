using DTech.Core.Terminal.Sessions;

namespace DTech.Core.CommandHistory;

public sealed class CommandCaptureSessionFactory(ICommandHistoryRecorder recorder, TimeProvider timeProvider)
{
    public CommandCaptureSession Create(ManagedTerminalSession session)
    {
        var target = new CommandCaptureTarget(session.Descriptor.ConnectionId, session.CommandHistoryNonce);
        return new CommandCaptureSession(target, recorder, timeProvider);
    }
}
