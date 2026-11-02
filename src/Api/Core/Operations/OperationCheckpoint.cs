namespace DTech.Core.Operations;

public readonly record struct OperationCheckpoint(Guid Id, string? RecoveryData, bool HasPendingCleanup);
