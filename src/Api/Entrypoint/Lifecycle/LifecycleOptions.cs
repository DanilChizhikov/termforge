namespace DTech.Entrypoint.Lifecycle;

internal sealed class LifecycleOptions
{
    public const string SectionName = "Lifecycle";

    public bool OpenBrowser { get; set; } = true;

    public string? BrowserCommand { get; set; }

    public int ShutdownTimeoutSeconds { get; set; } = 10;
}
