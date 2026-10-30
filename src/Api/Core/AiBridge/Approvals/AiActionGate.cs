using DTech.Core.AiBridge.Settings;

namespace DTech.Core.AiBridge.Approvals;

public sealed class AiActionGate(AiBridgeSettingsService settings, AiApprovalBroker broker)
{
    public async Task<AiBridgeError?> AuthorizeAsync(AiActionDescription action, CancellationToken cancellationToken)
    {
        var current = await settings.GetAsync(cancellationToken);
        if (!current.RequireApproval)
        {
            return null;
        }

        var outcome = await broker.RequestAsync(action, cancellationToken);
        return outcome switch
        {
            AiApprovalOutcome.Approved => null,
            AiApprovalOutcome.Denied => new AiBridgeError(AiBridgeErrorCode.Denied, $"The user denied this action on {action.ServerName}."),
            _ => new AiBridgeError(AiBridgeErrorCode.TimedOut, $"The user did not approve this action on {action.ServerName} in time."),
        };
    }
}
