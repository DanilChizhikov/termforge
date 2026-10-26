using DTech.Core.Connections;

namespace DTech.Ssh.IntegrationTests.Harness;

internal sealed class InMemoryTrustedHostKeyStore : ITrustedHostKeyStore
{
    private readonly Lock _gate = new();
    private readonly List<HostKeyVerificationRequest> _entries = [];

    public IReadOnlyList<HostKey> GetTrustedHostKeys(SshEndpoint endpoint)
    {
        lock (_gate)
        {
            return _entries.Where(entry => entry.Endpoint == endpoint).Select(entry => entry.HostKey).ToList();
        }
    }

    public Task AddAsync(HostKeyVerificationRequest request, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_entries.Contains(request))
            {
                _entries.Add(request);
            }
        }

        return Task.CompletedTask;
    }

    public Task ReplaceAsync(HostKeyVerificationRequest request, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _entries.RemoveAll(entry => entry.Endpoint == request.Endpoint && entry.HostKey.Algorithm == request.HostKey.Algorithm);
            _entries.Add(request);
        }

        return Task.CompletedTask;
    }
}
