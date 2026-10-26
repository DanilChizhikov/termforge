using DTech.Core.Diagnostics;
using DTech.Core.Diagnostics.Probes;

namespace DTech.Core.Files.Search;

public sealed class RemoteFindAvailability(CapabilityReportCache reportCache)
{
    private const string FindTool = "find";

    public async Task<string?> FindUnavailableReasonAsync(Guid connectionId, IRemoteCommandRunner runner, CancellationToken cancellationToken)
    {
        var execution = FindCachedExecutionCapability(connectionId);
        if (execution is { Status: CapabilityStatus.Unavailable })
        {
            return $"remote command execution is unavailable ({execution.Reason})";
        }

        var presence = await ToolLookup.FindAsync(runner, FindTool, cancellationToken);
        return presence switch
        {
            ToolPresence.Present => null,
            ToolPresence.Missing => "'find' is not installed on the server",
            _ => "could not check whether 'find' is installed",
        };
    }

    private CapabilityResult? FindCachedExecutionCapability(Guid connectionId)
    {
        var report = reportCache.Find(connectionId);
        if (report is null)
        {
            return null;
        }

        for (var i = 0; i < report.Capabilities.Count; i++)
        {
            var capability = report.Capabilities[i];
            if (capability.Id == CapabilityIds.CommandExecution)
            {
                return capability;
            }
        }

        return null;
    }
}
