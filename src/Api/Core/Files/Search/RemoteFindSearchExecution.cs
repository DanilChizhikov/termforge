using System.Runtime.CompilerServices;
using DTech.Core.Connections;
using DTech.Core.Diagnostics;

namespace DTech.Core.Files.Search;

public sealed class RemoteFindSearchExecution(IRemoteCommandRunner runner, FileSearchQuery query) : IFileSearchExecution
{
    public async IAsyncEnumerable<FileSearchEvent> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var tally = new FileSearchTally(FileSearchMethod.RemoteFind);
        var request = RemoteFindCommand.CreateRequest(query);
        var result = await RunAsync(request, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            yield return tally.Complete(FileSearchOutcome.Cancelled);
            yield break;
        }

        if (!result.IsSuccess)
        {
            yield return tally.Fail(result.Error!.Message);
            yield break;
        }

        var report = RemoteFindOutputParser.Parse(result.Value, query.Limits.MaxResults);
        for (var i = 0; i < report.Matches.Count; i++)
        {
            tally.RecordMatch();
            yield return report.Matches[i];
        }

        for (var i = 0; i < report.Warnings.Count; i++)
        {
            if (tally.RecordWarning())
            {
                yield return report.Warnings[i];
            }
        }

        yield return report.FailureMessage is null ? tally.Complete(report.Outcome) : tally.Fail(report.FailureMessage);
    }

    public ValueTask DisposeAsync()
    {
        return runner.DisposeAsync();
    }

    private async Task<SshResult<RemoteCommandResult>> RunAsync(RemoteCommandRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await runner.RunAsync(request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            var cancelled = new SshError(SshErrorKind.Cancelled, "Remote find was cancelled.");
            return SshResult<RemoteCommandResult>.Failure(cancelled);
        }
    }
}
