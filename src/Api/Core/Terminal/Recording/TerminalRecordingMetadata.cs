namespace DTech.Core.Terminal.Recording;

public sealed record TerminalRecordingMetadata
{
    public required TerminalRecordingId Id { get; init; }

    public required Guid ConnectionId { get; init; }

    public required string ConnectionName { get; init; }

    public required int SessionNumber { get; init; }

    public required TerminalSize Size { get; init; }

    public required DateTime StartedAt { get; init; }

    public DateTime? EndedAt { get; init; }

    public long SizeBytes { get; init; }

    public TerminalRecordingEndReason? EndReason { get; init; }

    public bool IsTruncated => EndReason is TerminalRecordingEndReason.SizeLimitReached or TerminalRecordingEndReason.WriterFellBehind or TerminalRecordingEndReason.StorageFailed;
}
