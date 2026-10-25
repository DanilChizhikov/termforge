namespace DTech.Entrypoint.Lifecycle;

internal sealed class ApplicationExitHandler(IHostApplicationLifetime lifetime, ILogger<ApplicationExitHandler> logger)
{
    public IResult RequestExit(HttpContext context)
    {
        logger.LogInformation("Application exit requested from the browser");
        context.Response.OnCompleted(StopApplication);
        return Results.Accepted();
    }

    private Task StopApplication()
    {
        lifetime.StopApplication();
        return Task.CompletedTask;
    }
}
