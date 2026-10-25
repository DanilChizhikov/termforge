using DTech.Core.Connections;
using DTech.Core.Terminal;
using DTech.Ssh.Connections;
using Renci.SshNet;

namespace DTech.Ssh.Terminal;

internal sealed class SshNetTerminalSessionFactory(SshClientConnector connector) : ITerminalSessionFactory
{
    private const int ShellBufferSize = 16 * 1024;

    public async Task<SshResult<ITerminalSession>> OpenAsync(TerminalSessionRequest request, CancellationToken cancellationToken)
    {
        if (!request.InitialSize.IsValid)
        {
            var invalidSizeError = new SshError(SshErrorKind.OperationFailed, $"Terminal size {request.InitialSize.Columns}x{request.InitialSize.Rows} is invalid");
            return SshResult<ITerminalSession>.Failure(invalidSizeError);
        }

        var connection = await connector.ConnectAsync(request.Target, CreateClient, cancellationToken);
        if (!connection.IsSuccess)
        {
            return SshResult<ITerminalSession>.Failure(connection.Error!);
        }

        ShellStream shellStream;
        try
        {
            var columns = (uint)request.InitialSize.Columns;
            var rows = (uint)request.InitialSize.Rows;
            shellStream = connection.Value.Client.CreateShellStream(request.TerminalType, columns, rows, 0, 0, ShellBufferSize);
        }
        catch (Exception exception)
        {
            connection.Value.Dispose();
            var error = SshExceptionMapper.Map(exception, "Opening interactive shell with PTY");
            var channelError = error with { Kind = SshErrorKind.ChannelFailed };
            return SshResult<ITerminalSession>.Failure(channelError);
        }

        var session = new SshNetTerminalSession(shellStream, connection.Value);
        return SshResult<ITerminalSession>.Success(session);
    }

    private static SshClient CreateClient(ConnectionInfo connectionInfo)
    {
        return new SshClient(connectionInfo);
    }
}
