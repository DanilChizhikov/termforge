using DTech.Core.Connections;
using Microsoft.Extensions.Logging;

namespace DTech.HostKeys.Decisions;

public sealed class HostKeyDecisionHandler(HostKeyDecisionBroker broker, ITrustedHostKeyStore trustedHostKeyStore, ILogger<HostKeyDecisionHandler> logger)
{
    public async Task<HostKeyDecisionResult> DecideAsync(HostKeyDecisionCommand command, CancellationToken cancellationToken)
    {
        var pendingDecision = broker.Find(command.PendingDecisionId);
        if (pendingDecision is null)
        {
            return NotFound(command.PendingDecisionId);
        }

        var challenge = pendingDecision.Challenge;
        var validation = Validate(challenge.Verification.Status, command);
        if (!validation.IsSuccess)
        {
            return validation;
        }

        await PersistAsync(challenge.Request, command.Decision, cancellationToken);
        var outcome = command.Decision == HostKeyUserDecision.Reject ? HostKeyPromptOutcome.Rejected : HostKeyPromptOutcome.Accepted;
        if (!broker.TryResolve(pendingDecision.Id, outcome))
        {
            return NotFound(command.PendingDecisionId);
        }

        var request = challenge.Request;
        logger.LogInformation($"Host key {request.HostKey} of {request.Endpoint}: {command.Decision}");
        return HostKeyDecisionResult.Success();
    }

    private static HostKeyDecisionResult Validate(HostKeyStatus status, HostKeyDecisionCommand command)
    {
        if (command.Decision == HostKeyUserDecision.Reject)
        {
            return HostKeyDecisionResult.Success();
        }

        if (status != HostKeyStatus.Changed)
        {
            return command.Decision == HostKeyUserDecision.Replace
                ? HostKeyDecisionResult.Failure(HostKeyDecisionErrorCode.NotAllowed, "Only a changed host key can be replaced; accept or reject this unknown key.")
                : HostKeyDecisionResult.Success();
        }

        if (command.Decision != HostKeyUserDecision.Replace)
        {
            return HostKeyDecisionResult.Failure(HostKeyDecisionErrorCode.NotAllowed, "A changed host key cannot be accepted; reject it or replace the saved key with explicit confirmation.");
        }

        return command.ConfirmReplace
            ? HostKeyDecisionResult.Success()
            : HostKeyDecisionResult.Failure(HostKeyDecisionErrorCode.ConfirmationRequired, "Replacing a changed host key requires explicit confirmation.");
    }

    private Task PersistAsync(HostKeyVerificationRequest request, HostKeyUserDecision decision, CancellationToken cancellationToken)
    {
        return decision switch
        {
            HostKeyUserDecision.AcceptAndSave => trustedHostKeyStore.AddAsync(request, cancellationToken),
            HostKeyUserDecision.Replace => trustedHostKeyStore.ReplaceAsync(request, cancellationToken),
            _ => Task.CompletedTask,
        };
    }

    private static HostKeyDecisionResult NotFound(Guid pendingDecisionId)
    {
        return HostKeyDecisionResult.Failure(HostKeyDecisionErrorCode.NotFound, $"No pending host key decision {pendingDecisionId}; it may have timed out or been decided already.");
    }
}
