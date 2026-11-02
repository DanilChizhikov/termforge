namespace DTech.Core.Operations;

public readonly record struct OperationRecoveryPolicy(DateTime Now, IReadOnlyCollection<string> RetryableKinds);
