using DTech.Core.Terminal.Recording;

namespace DTech.Entrypoint.Recordings;

internal sealed record TerminalRecordingResponse
{
    public required string Id { get; init; }

    public required Guid ConnectionId { get; init; }

    public required string ConnectionName { get; init; }

    public required int SessionNumber { get; init; }

    public required int Columns { get; init; }

    public required int Rows { get; init; }

    public required DateTime StartedAt { get; init; }

    public DateTime? EndedAt { get; init; }

    public required long SizeBytes { get; init; }

    public required string Status { get; init; }

    public string? EndReason { get; init; }

    public static TerminalRecordingResponse From(TerminalRecordingSummary summary)
    {
        var metadata = summary.Metadata;
        return new TerminalRecordingResponse
        {
            Id = metadata.Id.Value,
            ConnectionId = metadata.ConnectionId,
            ConnectionName = metadata.ConnectionName,
            SessionNumber = metadata.SessionNumber,
            Columns = metadata.Size.Columns,
            Rows = metadata.Size.Rows,
            StartedAt = metadata.StartedAt,
            EndedAt = metadata.EndedAt,
            SizeBytes = metadata.SizeBytes,
            Status = TerminalRecordingStatusNames.From(metadata, summary.IsActive),
            EndReason = TerminalRecordingStatusNames.EndReasonName(metadata.EndReason),
        };
    }
}
