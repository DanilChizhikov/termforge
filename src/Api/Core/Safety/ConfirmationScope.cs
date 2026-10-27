namespace DTech.Core.Safety;

public readonly record struct ConfirmationScope(DangerousOperation Operation, Guid ConnectionId, string Target);
