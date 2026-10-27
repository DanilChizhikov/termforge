namespace DTech.Core.Safety;

public sealed record WriteIntent(Guid ConnectionId)
{
    public DangerousOperation? Operation { get; init; }

    public string Target { get; init; } = string.Empty;

    public string? ConfirmationToken { get; init; }
}
