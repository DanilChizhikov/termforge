namespace DTech.Core.SavedConnections;

public sealed record ConnectionTestOptions(TimeSpan Timeout)
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    public static ConnectionTestOptions Default => new(DefaultTimeout);
}
