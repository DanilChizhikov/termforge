namespace DTech.Entrypoint.Security.Network;

internal static class NetworkHostName
{
    public static string Normalize(string host)
    {
        var trimmedHost = host.Trim();
        var isBracketed = trimmedHost.StartsWith('[') && trimmedHost.EndsWith(']');
        return isBracketed ? trimmedHost[1..^1] : trimmedHost;
    }

    public static bool IsValid(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var normalizedHost = Normalize(host);
        if (normalizedHost.Length == 0)
        {
            return false;
        }

        return Uri.CheckHostName(normalizedHost) != UriHostNameType.Unknown;
    }
}
