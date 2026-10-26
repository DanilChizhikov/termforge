namespace DTech.Entrypoint.HostKeys;

internal sealed record PendingHostKeyResponse(Guid Id, string Host, int Port, string Status)
{
    public required HostKeyResponse PresentedKey { get; init; }

    public required IReadOnlyList<HostKeyResponse> KnownKeys { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}
