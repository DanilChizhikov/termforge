using DTech.Core.Connections;
using Renci.SshNet;

namespace DTech.Ssh.Connections;

internal sealed class SshConnection<TClient> : IDisposable
    where TClient : BaseClient
{
    private readonly IReadOnlyList<IDisposable> _dependencies;
    private readonly SshConnectionHealth _health;
    private int _disposed;

    public SshConnection(TClient client, IReadOnlyList<IDisposable> dependencies, SshConnectionHealthOptions healthOptions)
    {
        Client = client;
        _dependencies = dependencies;
        _health = new SshConnectionHealth(client, healthOptions);
    }

    public TClient Client { get; }

    public CancellationToken Closed => _health.Closed;

    public SshError? Failure => _health.Failure;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        SshResourceCleanup.Dispose(_health, _dependencies);
    }
}
