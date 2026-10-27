using DTech.Core.Connections;
using DTech.Core.Terminal;
using DTech.Core.Terminal.Sessions;

namespace DTech.Ssh.IntegrationTests.Harness;

internal sealed class ManagedTerminalSessionAdapter(ManagedTerminalSession session) : ITerminalSession
{
    public bool IsOpen => !session.IsClosed;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        return session.ReadAsync(buffer, cancellationToken);
    }

    public ValueTask<SshResult> WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        return session.WriteAsync(data, cancellationToken);
    }

    public SshResult Resize(TerminalSize size)
    {
        return session.Resize(size);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
