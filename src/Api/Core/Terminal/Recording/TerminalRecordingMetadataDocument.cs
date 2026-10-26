namespace DTech.Core.Terminal.Recording;

internal sealed class TerminalRecordingMetadataDocument
{
    public string? Id { get; set; }

    public Guid ConnectionId { get; set; }

    public string? ConnectionName { get; set; }

    public int SessionNumber { get; set; }

    public int Columns { get; set; }

    public int Rows { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public long SizeBytes { get; set; }

    public string? EndReason { get; set; }
}
