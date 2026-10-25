namespace DTech.Entrypoint.Security;

internal static class SessionCookie
{
    public const string Name = "termforge_session";

    public static void Append(HttpContext context, string sessionToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = context.Request.IsHttps,
            Path = "/",
            IsEssential = true,
        };
        context.Response.Cookies.Append(Name, sessionToken, cookieOptions);
    }

    public static string? Read(HttpRequest request)
    {
        return request.Cookies[Name];
    }
}
