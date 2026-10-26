namespace DTech.Entrypoint.Security.Network;

internal readonly record struct LoginAttemptDecision(bool IsAllowed, TimeSpan RetryAfter)
{
    public static LoginAttemptDecision Allowed => new(true, TimeSpan.Zero);
}
