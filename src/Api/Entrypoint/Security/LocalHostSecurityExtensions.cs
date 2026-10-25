namespace DTech.Entrypoint.Security;

internal static class LocalHostSecurityExtensions
{
    public static WebApplicationBuilder AddLocalHostSecurity(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddTransient<IStartupFilter, LoopbackBindingStartupFilter>();
        services.AddSingleton<BootstrapToken>();
        services.AddSingleton<BrowserSessionStore>();
        services.AddSingleton<BootstrapExchangeHandler>();
        return builder;
    }

    public static WebApplication UseLocalHostSecurity(this WebApplication app)
    {
        app.UseMiddleware<HostHeaderValidationMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseWebSockets();
        app.UseMiddleware<OriginValidationMiddleware>();
        app.UseMiddleware<BrowserSessionMiddleware>();
        app.UseMiddleware<CsrfProtectionMiddleware>();
        app.MapBrowserSession();
        return app;
    }
}
