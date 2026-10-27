namespace DTech.Entrypoint.Lifecycle;

internal sealed class LifecycleOptions
{
    public const string SectionName = "Lifecycle";

    public bool OpenBrowser { get; set; } = true;

    public string? BrowserCommand { get; set; }

    public int ShutdownTimeoutSeconds { get; set; } = 10;

    public int IdleShutdownMinutes { get; set; }

    public int IdleCheckIntervalMilliseconds { get; set; } = 15000;

    public TimeSpan IdleCheckInterval => TimeSpan.FromMilliseconds(IdleCheckIntervalMilliseconds);

    public TimeSpan ShutdownTimeout => TimeSpan.FromSeconds(ShutdownTimeoutSeconds);

    public bool IsIdleShutdownEnabled => IdleShutdownMinutes > 0;

    public TimeSpan IdleShutdownTimeout => TimeSpan.FromMinutes(IdleShutdownMinutes);
}
