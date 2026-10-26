using DTech.Core.Diagnostics;

namespace DTech.Entrypoint.Diagnostics;

public sealed class CapabilityReportResponse
{
    public required Guid ConnectionId { get; init; }

    public required string State { get; init; }

    public required DateTime CheckedAt { get; init; }

    public string? Message { get; init; }

    public required IReadOnlyList<CapabilityResponse> Capabilities { get; init; }

    public static CapabilityReportResponse From(CapabilityReport report)
    {
        var capabilities = new List<CapabilityResponse>(report.Capabilities.Count);
        for (var i = 0; i < report.Capabilities.Count; i++)
        {
            var capability = CapabilityResponse.From(report.Capabilities[i]);
            capabilities.Add(capability);
        }

        return new CapabilityReportResponse
        {
            ConnectionId = report.ConnectionId,
            State = report.State.ToString(),
            CheckedAt = report.CheckedAt,
            Message = report.Message,
            Capabilities = capabilities,
        };
    }
}
