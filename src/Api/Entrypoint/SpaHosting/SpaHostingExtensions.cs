namespace DTech.Entrypoint.SpaHosting;

public static class SpaHostingExtensions
{
    private const string IndexFileName = "index.html";

    public static WebApplication UseSpaHosting(this WebApplication app)
    {
        var indexFile = app.Environment.WebRootFileProvider.GetFileInfo(IndexFileName);
        if (!indexFile.Exists)
        {
            app.MapGet("/", () => "TermForge: frontend is not built. See README.");
            return app;
        }

        app.UseMiddleware<SpaFallbackMiddleware>();
        app.UseStaticFiles();
        return app;
    }
}
