namespace DTech.Core.Operations;

public sealed record Operation
{
    public required Guid Id { get; init; }

    public required string Kind { get; init; }

    public string? Server { get; init; }

    public required OperationStatus Status { get; init; }

    public required DateTime CreatedAt { get; init; }

    public DateTime? StartedAt { get; init; }

    public DateTime? FinishedAt { get; init; }

    public required DateTime UpdatedAt { get; init; }

    public IReadOnlyDictionary<string, string> Details { get; init; } = new Dictionary<string, string>();

    public string? Error { get; init; }

    public bool Retryable { get; init; }
}
