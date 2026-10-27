using Renci.SshNet;

namespace DTech.Ssh.Connections;

internal sealed class SshConnection<TClient>(TClient client, IReadOnlyList<IDisposable> dependencies) : IDisposable
    where TClient : BaseClient
{
    private int _disposed;

    public TClient Client { get; } = client;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        Client.Dispose();
        for (var i = 0; i < dependencies.Count; i++)
        {
            var dependency = dependencies[i];
            dependency.Dispose();
        }
    }
}
