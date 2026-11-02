using DTech.Core.Diagnostics;
using DTech.Ssh.Connections;
using DTech.Ssh.Terminal;
using Renci.SshNet;

namespace DTech.Ssh.Commands;

internal sealed class SshNetRemoteCommandStream : IRemoteCommandStream
{
    private readonly SshConnection<SshClient> _connection;
    private readonly SshCommand _command;
    private readonly CancellationTokenSource _stopping = new();
    private readonly ShellOutputPump _outputPump;
    private readonly Task _execution;
    private readonly Task _errorDrain;
    private Exception? _failure;
    private int _commandStopped;
    private int _disposed;

    public SshNetRemoteCommandStream(SshConnection<SshClient> connection, SshCommand command)
    {
        _connection = connection;
        _command = command;
        _outputPump = new ShellOutputPump(command.OutputStream, StopCommand);
        _errorDrain = Task.Factory.StartNew(DrainErrors, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        _execution = RunAsync();
    }

    public int? ExitCode => _execution.IsCompleted ? _command.ExitStatus : null;

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var read = await _outputPump.ReadAsync(buffer, cancellationToken);
        if (read == 0)
        {
            await _execution.WaitAsync(cancellationToken);
            var failure = _outputPump.Failure ?? Volatile.Read(ref _failure);
            if (failure is not null)
            {
                throw new IOException("The SSH command output stream failed.", failure);
            }
        }

        return read;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        try
        {
            StopCommand();
        }
        finally
        {
            try
            {
                await _stopping.CancelAsync();
            }
            finally
            {
                var outputStopped = _outputPump.StopAsync();
                try
                {
                    await Task.WhenAll(_execution, _errorDrain, outputStopped);
                }
                finally
                {
                    _stopping.Dispose();
                }
            }
        }
    }

    private async Task RunAsync()
    {
        try
        {
            await _command.ExecuteAsync(_stopping.Token);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (!_stopping.IsCancellationRequested)
            {
                Interlocked.CompareExchange(ref _failure, exception, null);
            }
        }
        finally
        {
            _command.OutputStream.Dispose();
            _command.ExtendedOutputStream.Dispose();
        }
    }

    private void DrainErrors()
    {
        var buffer = new byte[16 * 1024];
        try
        {
            while (_command.ExtendedOutputStream.Read(buffer, 0, buffer.Length) > 0)
            {
                if (_command.ExtendedOutputStream.Length > 1024 * 1024)
                {
                    var overflow = new IOException("The SSH stderr backlog exceeded 1 MiB; the stream was closed.");
                    Interlocked.CompareExchange(ref _failure, overflow, null);
                    StopCommand();
                    return;
                }
            }
        }
        catch (Exception exception) when (exception is ObjectDisposedException or IOException)
        {
            if (!_stopping.IsCancellationRequested)
            {
                Interlocked.CompareExchange(ref _failure, exception, null);
            }
        }
    }

    private void StopCommand()
    {
        if (Interlocked.Exchange(ref _commandStopped, 1) == 1)
        {
            return;
        }

        try
        {
            _connection.Dispose();
        }
        finally
        {
            _command.Dispose();
        }
    }
}
