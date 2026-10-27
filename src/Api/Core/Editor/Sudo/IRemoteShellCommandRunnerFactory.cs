using DTech.Core.Connections;

namespace DTech.Core.Editor.Sudo;

public interface IRemoteShellCommandRunnerFactory
{
    Task<SshResult<IRemoteShellCommandRunner>> ConnectAsync(SshConnectionTarget target, CancellationToken cancellationToken);
}
