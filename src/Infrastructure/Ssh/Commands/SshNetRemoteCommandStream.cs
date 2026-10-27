using DTech.Core.Diagnostics;
using DTech.Ssh.Connections;
using DTech.Ssh.Terminal;
using Renci.SshNet;

namespace DTech.Ssh.Commands;

internal sealed class SshNetRemoteCommandStream : IRemoteCommandStream
{
    private static readonly TimeSpan StopGracePeriod = TimeSpan.FromSeconds(2);

    private readonly SshConnection<SshClient> _connection;
    private readonly SshCommand _command;
    private readonly CancellationTokenSource _stopping = new();
    private readonly ShellOutputPump _outputPump;
    private readonly Task _execution;
    private int _disposed;

    public SshNetRemoteCommandStream(SshConnection<SshClient> connection, SshCommand command)
    {
        _connection = connection;
        _command = command;
        _outputPump = new ShellOutputPump(command.OutputStream);
        _execution = RunAsync();
    }

    public int? ExitCode => _execution.IsCompleted ? _command.ExitStatus : null;

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var read = await _outputPump.ReadAsync(buffer, cancellationToken);
        if (read == 0)
        {
            await WaitForExecutionAsync();
        }

        return read;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await _stopping.CancelAsync();
        await WaitForExecutionAsync();
        _connection.Dispose();
        await _outputPump.StopAsync();
        _command.Dispose();
        _stopping.Dispose();
    }

    private async Task RunAsync()
    {
        try
        {
            await _command.ExecuteAsync(_stopping.Token);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
        }
        finally
        {
            _command.OutputStream.Dispose();
        }
    }

    private async Task WaitForExecutionAsync()
    {
        var delay = Task.Delay(StopGracePeriod, CancellationToken.None);
        await Task.WhenAny(_execution, delay);
    }
}
