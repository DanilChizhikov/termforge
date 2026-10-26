namespace DTech.Core.Reconnect;

public static class ReconnectBackoff
{
    public static TimeSpan DelayBefore(ReconnectPolicy policy, int attemptNumber)
    {
        if (attemptNumber < 1)
        {
            return TimeSpan.Zero;
        }

        var seconds = (double)policy.InitialDelaySeconds;
        for (var i = 1; i < attemptNumber && seconds < policy.MaxDelaySeconds; i++)
        {
            seconds *= 2;
        }

        var capped = Math.Min(seconds, policy.MaxDelaySeconds);
        return TimeSpan.FromSeconds(capped);
    }
}
