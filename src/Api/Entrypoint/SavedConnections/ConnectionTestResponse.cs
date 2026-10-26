using DTech.Core.SavedConnections;

namespace DTech.Entrypoint.SavedConnections;

public sealed class ConnectionTestResponse
{
    public required string State { get; init; }

    public required DateTime TestedAt { get; init; }

    public string? Message { get; init; }

    public string? Detail { get; init; }

    public long? LatencyMilliseconds { get; init; }

    public DateTime? LastConnectedAt { get; init; }

    public static ConnectionTestResponse From(ConnectionTestResult result)
    {
        return new ConnectionTestResponse
        {
            State = result.State.ToString(),
            TestedAt = result.TestedAt,
            Message = result.Message,
            Detail = result.Detail,
            LatencyMilliseconds = result.LatencyMilliseconds,
            LastConnectedAt = result.LastConnectedAt,
        };
    }
}
