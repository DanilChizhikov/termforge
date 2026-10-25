using DTech.Core.Operations;

namespace DTech.Entrypoint.Operations;

public sealed record OperationResponse(
    Guid Id,
    string Kind,
    string? Server,
    string Status,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    DateTime UpdatedAt,
    IReadOnlyDictionary<string, string> Details,
    string? Error,
    bool Retryable)
{
    public static OperationResponse From(Operation operation)
    {
        var status = operation.Status.ToString().ToLowerInvariant();
        return new OperationResponse(
            operation.Id,
            operation.Kind,
            operation.Server,
            status,
            operation.CreatedAt,
            operation.StartedAt,
            operation.FinishedAt,
            operation.UpdatedAt,
            operation.Details,
            operation.Error,
            operation.Retryable);
    }
}
