using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.Security.Network;

internal sealed class AllowedHostPolicy
{
    private readonly HashSet<string> _networkHosts = new(StringComparer.OrdinalIgnoreCase);

    public AllowedHostPolicy(IOptions<NetworkAccessOptions> options)
    {
        var networkAccess = options.Value;
        if (!networkAccess.Enabled)
        {
            return;
        }

        var allowedHosts = networkAccess.AllowedHosts;
        for (var i = 0; i < allowedHosts.Count; i++)
        {
            var allowedHost = allowedHosts[i];
            if (NetworkHostName.IsValid(allowedHost))
            {
                var normalizedHost = NetworkHostName.Normalize(allowedHost);
                _networkHosts.Add(normalizedHost);
            }
        }
    }

    public bool IsAllowed(string? host)
    {
        if (LoopbackHost.IsLoopback(host))
        {
            return true;
        }

        if (string.IsNullOrEmpty(host))
        {
            return false;
        }

        var normalizedHost = NetworkHostName.Normalize(host);
        return _networkHosts.Contains(normalizedHost);
    }
}
