namespace DTech.Entrypoint.Security;

internal static class SecurityPaths
{
    public const string ApiPrefix = "/api";
    public const string BootstrapPage = "/bootstrap";
    public const string BootstrapScript = "/bootstrap.js";
    public const string BootstrapExchange = "/api/session/bootstrap";
    public const string Session = "/api/session";
    public const string Login = "/api/session/login";
    public const string Logout = "/api/session/logout";

    public static bool IsApi(PathString path)
    {
        return path.StartsWithSegments(ApiPrefix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsBootstrapExchange(PathString path)
    {
        return path.Equals(BootstrapExchange, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsLogin(PathString path)
    {
        return path.Equals(Login, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSessionEstablishment(PathString path)
    {
        return IsBootstrapExchange(path) || IsLogin(path);
    }
}
