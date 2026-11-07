namespace DTech.Core.NetworkDiagnostics;

public sealed record RemoteNetworkActionRequest
{
    public Guid ConnectionId { get; init; }
    public string Kind { get; init; } = "install";
    public IReadOnlyList<string> Protocols { get; init; } = [];
    public string? Host { get; init; }
    public int? Port { get; init; }
    public bool UsePassword { get; init; }
}

public sealed class RemoteNetworkActionExecution
{
    public Guid PlanId { get; init; }
    public string? ConfirmationToken { get; init; }
    public string? Password { get; init; }
    public override string ToString() => "[redacted]";
}

public sealed record RemoteNetworkActionPlan
{
    public required Guid Id { get; init; }
    public required Guid ConnectionId { get; init; }
    public required string ServerName { get; init; }
    public required string Kind { get; init; }
    public required IReadOnlyList<string> Packages { get; init; }
    public required string Command { get; init; }
    public required bool RequiresSudo { get; init; }
    public required string ConfirmationToken { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public string Warning => Kind == "install" ? "Cancelling installation does not roll back packages already changed on the server."
        : "UDP silence is inconclusive and does not establish network blocking.";
}

public sealed record RemoteNetworkActionResult(NetworkProbeOutcome Outcome, string Capabilities);
