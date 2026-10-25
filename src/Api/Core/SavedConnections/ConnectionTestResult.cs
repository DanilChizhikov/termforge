namespace DTech.Core.SavedConnections;

public sealed record ConnectionTestResult(ConnectionTestState State, DateTime TestedAt)
{
    public string? Message { get; init; }

    public string? Detail { get; init; }

    public long? LatencyMilliseconds { get; init; }

    public DateTime? LastConnectedAt { get; init; }
}
