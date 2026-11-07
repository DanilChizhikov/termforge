using DTech.Core.Operations;
using DTech.Core.Safety;
using DTech.Core.Transfers;

namespace DTech.Entrypoint.Transfers;

internal sealed class TransferRetryConfirmationIssuer(IOperationJournal journal, ConfirmationIssuer issuer)
{
    public async Task<ConfirmationGrant> IssueAsync(Guid id, CancellationToken cancellationToken)
    {
        var operation = await journal.Find(id, cancellationToken);
        if (operation is null)
        {
            throw new OperationException(OperationFailure.NotFound, "The operation was not found.");
        }

        if (!operation.Retryable || operation.Status is not (OperationStatus.Failed or OperationStatus.Cancelled))
        {
            throw new OperationException(OperationFailure.NotRetryable, "This operation cannot be retried.");
        }

        var request = TransferOperationDetails.TryRead(operation);
        if (operation.Kind is not (TransferOperationKinds.Upload or TransferOperationKinds.Download or TransferOperationKinds.RemoteToRemote) || request is null)
        {
            throw new OperationException(OperationFailure.InvalidRequest, "The operation does not describe a transfer.");
        }

        var dangerousOperation = request.Direction != TransferDirection.Download
            ? DangerousOperation.TransferOverwrite
            : DangerousOperation.LocalTransferOverwrite;
        var scope = new ConfirmationScope(dangerousOperation, request.DestinationConnectionId, request.TargetDirectory);
        var issued = await issuer.IssueAsync(scope, cancellationToken);
        if (issued.Status == ConfirmationIssueStatus.ConnectionNotFound)
        {
            throw new OperationException(OperationFailure.InvalidRequest, "The saved connection was not found.");
        }

        if (issued.Status == ConfirmationIssueStatus.ReadOnlyServer)
        {
            throw new OperationException(OperationFailure.ReadOnlyServer, ServerWriteGate.ReadOnlyMessage);
        }

        return issued.Grant!;
    }
}
