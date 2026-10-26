namespace DTech.Entrypoint.Security;

internal sealed record BrowserSession(string SessionToken, string CsrfToken)
{
    public BrowserSessionKind Kind { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public bool IsExpiredAt(DateTimeOffset now)
    {
        return ExpiresAt is not null && ExpiresAt.Value <= now;
    }
}
