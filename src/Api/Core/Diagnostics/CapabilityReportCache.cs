using System.Collections.Concurrent;

namespace DTech.Core.Diagnostics;

public sealed class CapabilityReportCache
{
    private readonly ConcurrentDictionary<Guid, CapabilityReport> _reports = new();

    public CapabilityReport? Find(Guid connectionId)
    {
        return _reports.GetValueOrDefault(connectionId);
    }

    public void Store(CapabilityReport report)
    {
        _reports[report.ConnectionId] = report;
    }
}
