namespace DTech.Core.Operations;

public readonly record struct OperationRetentionPolicy(DateTime OlderThan, int MaximumCompleted);
