namespace DTech.Core.Credentials;

public sealed class CredentialMutationCoordinator : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        return new MutationLease(_gate);
    }

    public void Dispose()
    {
        _gate.Dispose();
    }

    private sealed class MutationLease(SemaphoreSlim gate) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                gate.Release();
            }
        }
    }
}
