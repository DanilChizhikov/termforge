using DTech.Core.Workspace;
using DTech.Entrypoint.Lifecycle;

namespace DTech.Entrypoint.Workspace;

internal sealed class WorkspaceShutdownStep(WorkspaceStateWriteCoordinator writeCoordinator) : IShutdownStep
{
    public ShutdownStage Stage => ShutdownStage.FlushWorkspace;

    public Task RunAsync(CancellationToken cancellationToken)
    {
        return writeCoordinator.FlushAsync(cancellationToken);
    }
}
