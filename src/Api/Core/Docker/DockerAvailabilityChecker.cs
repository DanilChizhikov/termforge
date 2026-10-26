using DTech.Core.Diagnostics;
using DTech.Core.Diagnostics.Probes;

namespace DTech.Core.Docker;

public sealed class DockerAvailabilityChecker(DockerProbe probe)
{
    private const string MissingResultReason = "Docker availability could not be determined.";

    public async Task<DockerAvailability> CheckAsync(CapabilityProbeContext context, CancellationToken cancellationToken)
    {
        var results = await probe.ProbeAsync(context, cancellationToken);
        var installed = Find(results, CapabilityIds.DockerInstalled);
        var access = Find(results, CapabilityIds.DockerAccess);
        if (installed is null || access is null)
        {
            return new DockerAvailability(DockerAvailabilityState.Unknown, MissingResultReason);
        }

        if (installed.Status == CapabilityStatus.Unavailable)
        {
            return new DockerAvailability(DockerAvailabilityState.NotInstalled, installed.Reason);
        }

        var state = access.Status switch
        {
            CapabilityStatus.Available => DockerAvailabilityState.Available,
            CapabilityStatus.Unavailable => DockerAvailabilityState.NoAccess,
            _ => DockerAvailabilityState.Unknown,
        };
        return new DockerAvailability(state, access.Reason);
    }

    private static CapabilityResult? Find(IReadOnlyList<CapabilityResult> results, string id)
    {
        for (var i = 0; i < results.Count; i++)
        {
            if (results[i].Id == id)
            {
                return results[i];
            }
        }

        return null;
    }
}
