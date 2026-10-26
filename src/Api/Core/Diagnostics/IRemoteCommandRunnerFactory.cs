using DTech.Core.Connections;

namespace DTech.Core.Diagnostics;

public interface IRemoteCommandRunnerFactory
{
    Task<SshResult<IRemoteCommandRunner>> ConnectAsync(SshConnectionTarget target, CancellationToken cancellationToken);
}
