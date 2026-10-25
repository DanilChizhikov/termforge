namespace DTech.Entrypoint.Security;

internal sealed class BootstrapExchangeHandler(BootstrapToken bootstrapToken, BrowserSessionStore sessionStore, ILogger<BootstrapExchangeHandler> logger)
{
    public IResult Exchange(BootstrapExchangeRequest request, HttpContext context)
    {
        if (!bootstrapToken.TryConsume(request.Token))
        {
            logger.LogWarning("Rejected a bootstrap token exchange: the token is invalid or was already used");
            return Results.Text("The launch link is invalid or was already used", statusCode: StatusCodes.Status403Forbidden);
        }

        var session = sessionStore.Create();
        SessionCookie.Append(context, session.SessionToken);
        logger.LogInformation("Browser session created");
        return Results.NoContent();
    }
}
