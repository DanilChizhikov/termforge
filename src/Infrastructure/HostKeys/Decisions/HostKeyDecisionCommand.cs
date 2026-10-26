namespace DTech.HostKeys.Decisions;

public sealed record HostKeyDecisionCommand(Guid PendingDecisionId, HostKeyUserDecision Decision, bool ConfirmReplace);
