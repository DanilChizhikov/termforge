using DTech.Core.Diagnostics.Parsing;

namespace DTech.Core.Diagnostics.Probes;

public sealed class DockerProbe : ICapabilityProbe
{
    private const string InstalledName = "Docker installed";
    private const string AccessName = "Docker access";

    public async Task<IReadOnlyList<CapabilityResult>> ProbeAsync(CapabilityProbeContext context, CancellationToken cancellationToken)
    {
        var presence = await ToolLookup.FindAsync(context.Runner, "docker", cancellationToken);
        if (presence == ToolPresence.Unknown)
        {
            return CreateUnknown("Could not check whether Docker is installed.");
        }

        if (presence == ToolPresence.Missing)
        {
            var installed = CapabilityResult.Unavailable(CapabilityIds.DockerInstalled, InstalledName, DockerAccessMessages.NotInstalled);
            var access = CapabilityResult.Unavailable(CapabilityIds.DockerAccess, AccessName, DockerAccessMessages.NotInstalled);
            return [installed, access];
        }

        var installedResult = CapabilityResult.Available(CapabilityIds.DockerInstalled, InstalledName, "The docker command is installed.");
        var accessResult = await ProbeAccessAsync(context, cancellationToken);
        return [installedResult, accessResult];
    }

    private static IReadOnlyList<CapabilityResult> CreateUnknown(string reason)
    {
        var installed = CapabilityResult.Unknown(CapabilityIds.DockerInstalled, InstalledName, reason);
        var access = CapabilityResult.Unknown(CapabilityIds.DockerAccess, AccessName, reason);
        return [installed, access];
    }

    private static async Task<CapabilityResult> ProbeAccessAsync(CapabilityProbeContext context, CancellationToken cancellationToken)
    {
        var result = await context.Runner.RunScriptAsync(DockerAccessParser.Script, cancellationToken);
        if (!result.IsSuccess)
        {
            var failure = ToolLookup.DescribeFailure("query the Docker daemon", result.Error?.Message);
            return CapabilityResult.Unknown(CapabilityIds.DockerAccess, AccessName, failure);
        }

        var info = DockerAccessParser.Parse(result.Value);
        return info.Access switch
        {
            DockerAccess.Accessible => CapabilityResult.Available(CapabilityIds.DockerAccess, AccessName, $"The Docker daemon is reachable (server version {info.Version})."),
            DockerAccess.PermissionDenied => CapabilityResult.Unavailable(CapabilityIds.DockerAccess, AccessName, DockerAccessMessages.PermissionDenied),
            DockerAccess.DaemonNotRunning => CapabilityResult.Unavailable(CapabilityIds.DockerAccess, AccessName, DockerAccessMessages.DaemonNotRunning),
            _ => CapabilityResult.Unknown(CapabilityIds.DockerAccess, AccessName, "Docker returned an unrecognized answer to 'docker info'."),
        };
    }
}
