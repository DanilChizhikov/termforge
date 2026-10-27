using DTech.Core.Connections;
using DTech.Core.Editor.Sudo;
using DTech.Ssh.Commands;
using DTech.Ssh.Connections;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace DTech.Ssh.Shell;

internal sealed class SshNetRemoteShellCommandRunner(SshConnection<SshClient> connection) : IRemoteShellCommandRunner
{
    private const string Operation = "Running remote command";
    private const int MaxErrorBytes = 64 * 1024;

    private static readonly TimeSpan DrainGracePeriod = TimeSpan.FromSeconds(2);

    public async Task<SshResult<RemoteShellCommandResult>> RunAsync(RemoteShellCommand command, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(command.Timeout);
        try
        {
            return await ExecuteAsync(command, limit, cancellationToken);
        }
        catch (Exception exception)
        {
            var error = SshExceptionMapper.Map(exception, Operation);
            return SshResult<RemoteShellCommandResult>.Failure(error);
        }
    }

    public ValueTask DisposeAsync()
    {
        connection.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<SshResult<RemoteShellCommandResult>> ExecuteAsync(RemoteShellCommand command, CancellationTokenSource limit, CancellationToken cancellationToken)
    {
        using var sshCommand = connection.Client.CreateCommand(command.Text);
        var standardOutput = new BoundedStreamCollector(sshCommand.OutputStream, command.MaxOutputBytes, limit.Cancel);
        var standardError = new BoundedStreamCollector(sshCommand.ExtendedOutputStream, MaxErrorBytes, limit.Cancel);
        var outputCollection = standardOutput.CollectAsync();
        var errorCollection = standardError.CollectAsync();
        var collectors = Task.WhenAll(outputCollection, errorCollection);
        var execution = sshCommand.ExecuteAsync(limit.Token);
        await WriteInputAsync(sshCommand, command.StandardInput, limit.Token);
        var failure = await AwaitExecutionAsync(execution);
        var drainDelay = Task.Delay(DrainGracePeriod, CancellationToken.None);
        await Task.WhenAny(collectors, drainDelay);

        var overflowed = standardOutput.Overflowed || standardError.Overflowed;
        if (failure is not null && !overflowed)
        {
            var described = DescribeFailure(failure, cancellationToken);
            return SshResult<RemoteShellCommandResult>.Failure(described);
        }

        var exitCode = sshCommand.ExitStatus ?? -1;
        var outputBytes = standardOutput.GetBytes();
        var errorText = standardError.GetText();
        var result = new RemoteShellCommandResult(exitCode, outputBytes, errorText)
        {
            OutputTruncated = overflowed,
        };
        return SshResult<RemoteShellCommandResult>.Success(result);
    }

    private static async Task WriteInputAsync(SshCommand sshCommand, ReadOnlyMemory<byte> standardInput, CancellationToken cancellationToken)
    {
        try
        {
            await using var input = sshCommand.CreateInputStream();
            if (!standardInput.IsEmpty)
            {
                await input.WriteAsync(standardInput, cancellationToken);
                await input.FlushAsync(cancellationToken);
            }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException or InvalidOperationException or SshException or OperationCanceledException)
        {
        }
    }

    private static async Task<Exception?> AwaitExecutionAsync(Task execution)
    {
        try
        {
            await execution;
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static SshError DescribeFailure(Exception failure, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            var cancelled = new OperationCanceledException(cancellationToken);
            return SshExceptionMapper.Map(cancelled, Operation);
        }

        var timedOut = failure is OperationCanceledException or SshOperationTimeoutException;
        if (timedOut)
        {
            return new SshError(SshErrorKind.OperationFailed, $"{Operation} failed: the command timed out.");
        }

        return SshExceptionMapper.Map(failure, Operation);
    }
}
