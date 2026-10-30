namespace DTech.Core.Docker;

public sealed record ImageRemoveRequest(Guid ConnectionId, string? Image)
{
    public string? ConfirmationToken { get; init; }
}
