using DTech.Entrypoint.Security.Network;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;

namespace DTech.Entrypoint.Security;

internal static class BrowserSessionEndpoints
{
    private const string HtmlContentType = "text/html; charset=utf-8";
    private const string ScriptContentType = "text/javascript; charset=utf-8";

    public static IEndpointRouteBuilder MapBrowserSession(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(SecurityPaths.BootstrapPage, ServeBootstrapPage);
        endpoints.MapGet(SecurityPaths.BootstrapScript, ServeBootstrapScript);
        endpoints.MapPost(SecurityPaths.BootstrapExchange, ExchangeBootstrapToken);
        endpoints.MapGet(SecurityPaths.Session, GetSession);
        endpoints.MapGet(SecurityPaths.Login, GetLoginStatus);
        endpoints.MapPost(SecurityPaths.Login, Login);
        endpoints.MapPost(SecurityPaths.Logout, Logout);
        return endpoints;
    }

    private static IResult ServeBootstrapPage(HttpContext context)
    {
        SetBootstrapHeaders(context.Response);
        return Results.Text(BootstrapPage.Html, HtmlContentType);
    }

    private static IResult ServeBootstrapScript(HttpContext context)
    {
        SetBootstrapHeaders(context.Response);
        return Results.Text(BootstrapPage.Script, ScriptContentType);
    }

    private static IResult ExchangeBootstrapToken(BootstrapExchangeRequest request, HttpContext context, BootstrapExchangeHandler handler)
    {
        return handler.Exchange(request, context);
    }

    private static IResult GetSession(HttpContext context)
    {
        var session = context.Features.GetRequiredFeature<BrowserSession>();
        var response = new BrowserSessionResponse(session.CsrfToken);
        return Results.Ok(response);
    }

    private static IResult GetLoginStatus(HttpContext context, PasswordLoginHandler handler)
    {
        var status = handler.GetStatus(context);
        return Results.Ok(status);
    }

    private static IResult Login(PasswordLoginRequest request, HttpContext context, PasswordLoginHandler handler)
    {
        return handler.Login(request, context);
    }

    private static IResult Logout(HttpContext context, BrowserSessionStore sessionStore)
    {
        var session = context.Features.GetRequiredFeature<BrowserSession>();
        sessionStore.Remove(session.SessionToken);
        SessionCookie.Delete(context);
        return Results.NoContent();
    }

    private static void SetBootstrapHeaders(HttpResponse response)
    {
        response.Headers.ContentSecurityPolicy = BootstrapPage.ContentSecurityPolicy;
        response.Headers[HeaderNames.CacheControl] = "no-store";
    }
}
