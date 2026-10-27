using DTech.HostKeys.Decisions;

namespace DTech.Entrypoint.HostKeys;

internal readonly record struct HostKeyDecisionParameters(Guid Id, HostKeyDecisionRequest Request, HostKeyDecisionHandler Handler);
