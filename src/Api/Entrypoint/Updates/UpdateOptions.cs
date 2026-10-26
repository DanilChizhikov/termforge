namespace DTech.Entrypoint.Updates;

internal sealed class UpdateOptions
{
    public const string SectionName = "Updates";

    public bool CheckOnStartup { get; set; } = true;

    public string Repository { get; set; } = "DanilChizhikov/termforge";

    public string ApiBaseUrl { get; set; } = "https://api.github.com/";

    public int CacheMinutes { get; set; } = 360;

    public int FailureCacheMinutes { get; set; } = 5;

    public int TimeoutSeconds { get; set; } = 10;
}
