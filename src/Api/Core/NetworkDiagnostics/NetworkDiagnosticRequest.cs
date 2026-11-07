namespace DTech.Core.NetworkDiagnostics;

public sealed record NetworkDiagnosticRequest
{
    public Guid? TargetConnectionId { get; init; }
    public string? Host { get; init; }
    public string? Domain { get; init; }
    public string? Url { get; init; }
    public string? Ports { get; init; }
    public bool AllPorts { get; init; }
    public IReadOnlyList<string> Protocols { get; init; } = [];
    public IReadOnlyList<NetworkDiagnosticPoint> Points { get; init; } = [];
    public string? ConfirmationToken { get; init; }
}

public sealed record NetworkDiagnosticPoint(Guid? ConnectionId, string Category);

public sealed record NetworkDiagnosticPlan
{
    public required string Host { get; init; }
    public required string Domain { get; init; }
    public Uri? Url { get; init; }
    public required IReadOnlyList<int> Ports { get; init; }
    public required IReadOnlyList<string> Protocols { get; init; }
    public required IReadOnlyList<NetworkDiagnosticPoint> Points { get; init; }
    public bool FullScan => Ports.Count == 65535;
    public int ProbesPerPoint => Protocols.Sum(protocol => protocol is "dns" or "route" ? 1 : protocol == "http" && Url is not null ? 1 : Ports.Count);
}

public sealed record NetworkProbe
{
    public required string Host { get; init; }
    public required string Domain { get; init; }
    public required string Protocol { get; init; }
    public int? Port { get; init; }
    public Uri? Url { get; init; }
}

public sealed record NetworkProbeOutcome(string Status, string Detail)
{
    public const string Responded = "responded";
    public const string Refused = "refused";
    public const string NoResponse = "noResponse";
    public const string Error = "error";
    public const string Unavailable = "unavailable";
}

public interface INetworkProbeSession : IAsyncDisposable
{
    Task<NetworkProbeOutcome> ProbeAsync(NetworkProbe probe, CancellationToken cancellationToken);
}
