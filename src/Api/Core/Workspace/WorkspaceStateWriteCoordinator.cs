namespace DTech.Core.Workspace;

public sealed class WorkspaceStateWriteCoordinator : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task RunAsync(Func<CancellationToken, Task> write, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await write(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        _gate.Release();
    }

    public void Dispose()
    {
        _gate.Dispose();
    }
}
