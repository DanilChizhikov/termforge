using System.Text;
using DTech.Core.Terminal.Sessions;

namespace DTech.Core.Docker;

public sealed class ContainerShellLauncher(ITerminalLauncher launcher, TerminalSessionManager sessionManager)
{
    public async Task<TerminalResult<ManagedTerminalSession>> LaunchAsync(ContainerShellRequest request, CancellationToken cancellationToken)
    {
        if (!ContainerReference.TryParse(request.Container, out var container))
        {
            var invalidContainer = DockerError.InvalidContainer();
            var invalid = new TerminalError(TerminalErrorCode.InvalidRequest, invalidContainer.Message);
            return TerminalResult<ManagedTerminalSession>.Failure(invalid);
        }

        var launched = await launcher.LaunchAsync(request.Launch, cancellationToken);
        if (!launched.IsSuccess)
        {
            return launched;
        }

        var startupLine = DockerCommands.ShellStartupLine(container, request.Shell);
        var startupBytes = Encoding.UTF8.GetBytes(startupLine);
        var written = await launched.Value.WriteAsync(startupBytes, cancellationToken);
        if (written.IsSuccess)
        {
            return launched;
        }

        await sessionManager.ReleaseAsync(launched.Value, TerminalCloseReasons.RemoteShellExited);
        var writeError = TerminalError.FromSsh(written.Error!);
        return TerminalResult<ManagedTerminalSession>.Failure(writeError);
    }
}
