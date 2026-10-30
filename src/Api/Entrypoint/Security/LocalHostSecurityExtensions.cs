using DTech.Entrypoint.Security.Network;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DTech.Entrypoint.Security;

internal static class LocalHostSecurityExtensions
{
    public static WebApplicationBuilder AddLocalHostSecurity(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var networkAccessSection = builder.Configuration.GetSection(NetworkAccessOptions.SectionName);
        services.AddOptions<NetworkAccessOptions>().Bind(networkAccessSection);
        services.TryAddSingleton(TimeProvider.System);
        services.AddTransient<IStartupFilter, BindingSecurityStartupFilter>();
        services.AddSingleton<AllowedHostPolicy>();
        services.AddSingleton<LoginAttemptLimiter>();
        services.AddSingleton<NetworkPasswordVerifier>();
        services.AddSingleton<PasswordLoginHandler>();
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
        app.UseMiddleware<AiBridgeTokenMiddleware>();
        app.MapBrowserSession();
        return app;
    }
}
