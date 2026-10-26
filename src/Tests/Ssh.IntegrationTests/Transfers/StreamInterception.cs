namespace DTech.Ssh.IntegrationTests.Transfers;

internal sealed record StreamInterception
{
    public long? ReadLimit { get; init; }

    public long? WriteLimit { get; init; }

    public TimeSpan? Delay { get; init; }
}
