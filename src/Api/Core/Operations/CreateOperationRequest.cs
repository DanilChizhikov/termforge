namespace DTech.Core.Operations;

public readonly record struct CreateOperationRequest(
    string Kind,
    string? Server,
    IReadOnlyDictionary<string, string>? Details,
    bool Retryable);
