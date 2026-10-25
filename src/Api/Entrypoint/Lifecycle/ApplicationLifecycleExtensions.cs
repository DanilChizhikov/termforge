using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.Lifecycle;

internal static class ApplicationLifecycleExtensions
{
    public const string ExitPath = "/api/lifecycle/exit";

    public static WebApplicationBuilder AddApplicationLifecycle(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var lifecycleSection = builder.Configuration.GetSection(LifecycleOptions.SectionName);
        services.AddOptions<LifecycleOptions>()
            .Bind(lifecycleSection)
            .Validate(HasPositiveShutdownTimeout, "Lifecycle:ShutdownTimeoutSeconds must be greater than zero")
            .ValidateOnStart();
        services.AddOptions<HostOptions>().Configure<IOptions<LifecycleOptions>>(ApplyShutdownTimeout);
        services.AddSingleton<IBrowserLauncher, SystemBrowserLauncher>();
        services.AddSingleton<BootstrapUrlFactory>();
        services.AddSingleton<ApplicationExitHandler>();
        services.AddHostedService<BrowserOpeningService>();
        return builder;
    }

    public static IEndpointRouteBuilder MapApplicationLifecycle(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(ExitPath, RequestExit);
        return endpoints;
    }

    private static IResult RequestExit(HttpContext context, ApplicationExitHandler handler)
    {
        return handler.RequestExit(context);
    }

    private static bool HasPositiveShutdownTimeout(LifecycleOptions options)
    {
        return options.ShutdownTimeoutSeconds > 0;
    }

    private static void ApplyShutdownTimeout(HostOptions hostOptions, IOptions<LifecycleOptions> lifecycleOptions)
    {
        var shutdownTimeoutSeconds = lifecycleOptions.Value.ShutdownTimeoutSeconds;
        hostOptions.ShutdownTimeout = TimeSpan.FromSeconds(shutdownTimeoutSeconds);
    }
}
