namespace DTech.Core.Reconnect;

public static class ReconnectPolicyLimits
{
    public const int MinAttempts = 1;
    public const int MaxAttempts = 20;
    public const int MinDelaySeconds = 1;
    public const int MaxDelaySeconds = 3600;
}
