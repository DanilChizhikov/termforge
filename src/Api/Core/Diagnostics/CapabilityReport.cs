namespace DTech.Core.Diagnostics;

public sealed record CapabilityReport(Guid ConnectionId, DateTime CheckedAt, CapabilityReportState State, IReadOnlyList<CapabilityResult> Capabilities)
{
    public string? Message { get; init; }
}
