namespace DTech.Entrypoint.Security;

internal static class SecurityPaths
{
    public const string ApiPrefix = "/api";
    public const string BootstrapPage = "/bootstrap";
    public const string BootstrapScript = "/bootstrap.js";
    public const string BootstrapExchange = "/api/session/bootstrap";
    public const string Session = "/api/session";

    public static bool IsApi(PathString path)
    {
        return path.StartsWithSegments(ApiPrefix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsBootstrapExchange(PathString path)
    {
        return path.Equals(BootstrapExchange, StringComparison.OrdinalIgnoreCase);
    }
}
