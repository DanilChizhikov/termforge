namespace DTech.Core.Operations;

public sealed record OperationRetryOptions(bool Overwrite = false, string? ConfirmationToken = null);
