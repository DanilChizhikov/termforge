namespace DTech.Ssh.Connections;

internal sealed record SshConnectionHealthOptions(TimeSpan Interval, TimeSpan Timeout)
{
    public static SshConnectionHealthOptions Default { get; } = CreateDefault();

    public bool ProbeEnabled { get; init; } = true;

    private static SshConnectionHealthOptions CreateDefault()
    {
        var interval = TimeSpan.FromSeconds(30);
        var timeout = TimeSpan.FromSeconds(10);
        return new SshConnectionHealthOptions(interval, timeout);
    }
}
