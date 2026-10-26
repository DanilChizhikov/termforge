using DTech.Core.Connections;
using DTech.Core.Files;

namespace DTech.Core.Diagnostics.Probes;

public sealed class SftpProbe : ICapabilityProbe
{
    private const string Name = "SFTP file transfer";

    private readonly IRemoteFileClientFactory _fileClientFactory;

    public SftpProbe(IRemoteFileClientFactory fileClientFactory)
    {
        _fileClientFactory = fileClientFactory;
    }

    public async Task<IReadOnlyList<CapabilityResult>> ProbeAsync(CapabilityProbeContext context, CancellationToken cancellationToken)
    {
        var connected = await _fileClientFactory.ConnectAsync(context.Target, cancellationToken);
        if (connected.IsSuccess)
        {
            await connected.Value.DisposeAsync();
            return [CapabilityResult.Available(CapabilityIds.Sftp, Name, "The SFTP subsystem is enabled.")];
        }

        return [DescribeFailure(connected.Error!)];
    }

    private static CapabilityResult DescribeFailure(SshError error)
    {
        var isSubsystemProblem = error.Kind is SshErrorKind.ChannelFailed or SshErrorKind.OperationFailed or SshErrorKind.PermissionDenied;
        if (isSubsystemProblem)
        {
            return CapabilityResult.Unavailable(CapabilityIds.Sftp, Name, "The SFTP subsystem is not available. Enable 'Subsystem sftp' in sshd_config on the server and install the SFTP server package.");
        }

        var reason = ToolLookup.DescribeFailure("open an SFTP session", error.Message);
        return CapabilityResult.Unknown(CapabilityIds.Sftp, Name, reason);
    }
}
