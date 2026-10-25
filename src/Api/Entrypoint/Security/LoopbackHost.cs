using System.Net;

namespace DTech.Entrypoint.Security;

internal static class LoopbackHost
{
    private const string LocalhostName = "localhost";

    public static bool IsLoopback(string? host)
    {
        if (string.IsNullOrEmpty(host))
        {
            return false;
        }

        if (string.Equals(host, LocalhostName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var addressText = host.StartsWith('[') && host.EndsWith(']') ? host[1..^1] : host;
        return IPAddress.TryParse(addressText, out var address) && IPAddress.IsLoopback(address);
    }
}
