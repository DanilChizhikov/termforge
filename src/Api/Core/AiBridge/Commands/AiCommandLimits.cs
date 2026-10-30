namespace DTech.Core.AiBridge.Commands;

public static class AiCommandLimits
{
    public const int DefaultTimeoutSeconds = 60;
    public const int MaxTimeoutSeconds = 300;
    public const int MaxCommandLength = 8000;
    public const int MaxOutputBytes = 256 * 1024;

    public static TimeSpan TimeoutOf(int? requestedSeconds)
    {
        var seconds = Math.Clamp(requestedSeconds ?? DefaultTimeoutSeconds, 1, MaxTimeoutSeconds);
        return TimeSpan.FromSeconds(seconds);
    }
}
