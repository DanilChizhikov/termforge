namespace DTech.Core.Operations;

public readonly record struct FailOperationRequest(
    string Error,
    bool Retryable,
    IReadOnlyDictionary<string, string>? Details);
