namespace DTech.Core.SavedConnections;

public sealed record SavedConnection
{
    public required Guid Id { get; init; }

    public required SavedConnectionDraft Details { get; init; }

    public bool HasPassword { get; init; }

    public DateTime? LastConnectedAt { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}
