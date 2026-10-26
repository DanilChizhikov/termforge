namespace DTech.Core.Reconnect;

public sealed record ReconnectPolicy(ReconnectMode Mode, int MaxAttempts, int InitialDelaySeconds, int MaxDelaySeconds)
{
    public static ReconnectPolicy Default { get; } = new(ReconnectMode.Manual, 5, 2, 60);
}
