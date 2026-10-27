using DTech.Core.Connections;
using DTech.Core.Diagnostics;
using DTech.Ssh.Connections;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace DTech.Ssh.Commands;

internal sealed class SshNetRemoteCommandRunner : IRemoteCommandRunner
{
    private static readonly TimeSpan DrainGracePeriod = TimeSpan.FromSeconds(2);

    private readonly SshConnection<SshClient> _connection;

    public SshNetRemoteCommandRunner(SshConnection<SshClient> connection)
    {
        _connection = connection;
    }

    public async Task<SshResult<RemoteCommandResult>> RunAsync(RemoteCommandRequest request, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(request.Timeout);
        try
        {
            return await ExecuteAsync(request, limit, cancellationToken);
        }
        catch (Exception exception)
        {
            var error = SshExceptionMapper.Map(exception, "Running remote command");
            return SshResult<RemoteCommandResult>.Failure(error);
        }
    }

    public ValueTask DisposeAsync()
    {
        _connection.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<SshResult<RemoteCommandResult>> ExecuteAsync(RemoteCommandRequest request, CancellationTokenSource limit, CancellationToken cancellationToken)
    {
        using var command = _connection.Client.CreateCommand(request.Command);
        var standardOutput = new BoundedStreamCollector(command.OutputStream, request.MaxOutputBytes, limit.Cancel);
        var standardError = new BoundedStreamCollector(command.ExtendedOutputStream, request.MaxOutputBytes, limit.Cancel);
        var collectors = Task.WhenAll(standardOutput.CollectAsync(), standardError.CollectAsync());
        var execution = command.ExecuteAsync(limit.Token);
        var failure = await AwaitExecutionAsync(execution);
        await Task.WhenAny(collectors, Task.Delay(DrainGracePeriod, CancellationToken.None));

        var overflowed = standardOutput.Overflowed || standardError.Overflowed;
        if (failure is not null && !overflowed)
        {
            return SshResult<RemoteCommandResult>.Failure(DescribeFailure(failure, cancellationToken));
        }

        var exitCode = command.ExitStatus ?? -1;
        var result = new RemoteCommandResult(exitCode, standardOutput.GetText(), standardError.GetText())
        {
            OutputTruncated = overflowed,
        };
        return SshResult<RemoteCommandResult>.Success(result);
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
            return SshExceptionMapper.Map(new OperationCanceledException(cancellationToken), "Running remote command");
        }

        var timedOut = failure is OperationCanceledException or SshOperationTimeoutException;
        if (timedOut)
        {
            return new SshError(SshErrorKind.OperationFailed, "Running remote command failed: the command timed out.");
        }

        return SshExceptionMapper.Map(failure, "Running remote command");
    }
}
