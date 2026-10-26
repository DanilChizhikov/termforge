using DTech.Entrypoint.Security;
using Microsoft.AspNetCore.Http.Features;

namespace DTech.Entrypoint.Terminals;

internal static class TerminalOwner
{
    public static string From(HttpContext context)
    {
        var session = context.Features.GetRequiredFeature<BrowserSession>();
        return session.SessionToken;
    }
}
