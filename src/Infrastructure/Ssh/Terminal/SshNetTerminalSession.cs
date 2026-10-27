using DTech.Core.Connections;
using DTech.Core.Terminal;
using DTech.Ssh.Connections;
using Renci.SshNet;

namespace DTech.Ssh.Terminal;

internal sealed class SshNetTerminalSession : ITerminalSession
{
    private readonly ShellStream _shellStream;
    private readonly SshConnection<SshClient> _connection;
    private readonly ShellOutputPump _outputPump;
    private int _disposed;

    public SshNetTerminalSession(ShellStream shellStream, SshConnection<SshClient> connection)
    {
        _shellStream = shellStream;
        _connection = connection;
        _outputPump = new ShellOutputPump(shellStream);
    }

    public bool IsOpen => _disposed == 0 && _connection.Client.IsConnected && !_outputPump.IsCompleted;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        return _outputPump.ReadAsync(buffer, cancellationToken);
    }

    public async ValueTask<SshResult> WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        if (!IsOpen)
        {
            var closedError = new SshError(SshErrorKind.SessionClosed, "Terminal session is closed");
            return SshResult.Failure(closedError);
        }

        try
        {
            await _shellStream.WriteAsync(data, cancellationToken);
            _shellStream.Flush();
            return SshResult.Success();
        }
        catch (Exception exception)
        {
            var error = SshExceptionMapper.Map(exception, "Writing to terminal");
            return SshResult.Failure(error);
        }
    }

    public SshResult Resize(TerminalSize size)
    {
        if (!size.IsValid)
        {
            var invalidSizeError = new SshError(SshErrorKind.OperationFailed, $"Terminal size {size.Columns}x{size.Rows} is invalid");
            return SshResult.Failure(invalidSizeError);
        }

        if (!IsOpen)
        {
            var closedError = new SshError(SshErrorKind.SessionClosed, "Terminal session is closed");
            return SshResult.Failure(closedError);
        }

        try
        {
            var columns = (uint)size.Columns;
            var rows = (uint)size.Rows;
            _shellStream.ChangeWindowSize(columns, rows, 0, 0);
            return SshResult.Success();
        }
        catch (Exception exception)
        {
            var error = SshExceptionMapper.Map(exception, "Resizing terminal");
            return SshResult.Failure(error);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _shellStream.Dispose();
        await _outputPump.StopAsync();
        _connection.Dispose();
    }
}
