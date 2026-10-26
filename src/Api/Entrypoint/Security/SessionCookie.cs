namespace DTech.Entrypoint.Security;

internal static class SessionCookie
{
    public const string Name = "termforge_session";

    public static void Append(HttpContext context, string sessionToken)
    {
        var cookieOptions = CreateOptions(context);
        context.Response.Cookies.Append(Name, sessionToken, cookieOptions);
    }

    public static void Delete(HttpContext context)
    {
        var cookieOptions = CreateOptions(context);
        context.Response.Cookies.Delete(Name, cookieOptions);
    }

    public static string? Read(HttpRequest request)
    {
        return request.Cookies[Name];
    }

    private static CookieOptions CreateOptions(HttpContext context)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = context.Request.IsHttps,
            Path = "/",
            IsEssential = true,
        };
    }
}
