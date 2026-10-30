using DTech.Entrypoint.Security;

namespace DTech.Entrypoint.AiBridge;

internal static class AiBridgeUrls
{
    public static string McpUrlOf(HttpRequest request)
    {
        return $"{request.Scheme}://{request.Host.Value}{SecurityPaths.AiBridge}";
    }
}
