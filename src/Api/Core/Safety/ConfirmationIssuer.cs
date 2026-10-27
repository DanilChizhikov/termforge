using DTech.Core.SavedConnections;

namespace DTech.Core.Safety;

public sealed class ConfirmationIssuer(ISavedConnectionRepository repository, IConfirmationTokenStore confirmations)
{
    public async Task<ConfirmationIssueResult> IssueAsync(ConfirmationScope scope, CancellationToken cancellationToken)
    {
        var connection = await repository.FindAsync(scope.ConnectionId, cancellationToken);
        if (connection is null)
        {
            return new ConfirmationIssueResult(ConfirmationIssueStatus.ConnectionNotFound, null, null);
        }

        if (connection.Details.ReadOnly)
        {
            return new ConfirmationIssueResult(ConfirmationIssueStatus.ReadOnlyServer, null, connection.Details.Name);
        }

        var grant = confirmations.Issue(scope);
        return new ConfirmationIssueResult(ConfirmationIssueStatus.Issued, grant, connection.Details.Name);
    }
}
