using DTech.Entrypoint.Realtime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.Lifecycle;

internal static class ApplicationLifecycleExtensions
{
    public const string LifecyclePath = "/api/lifecycle";
    public const string ExitPath = "/api/lifecycle/exit";
    public const string AttentionPath = "/api/lifecycle/attention";

    public static WebApplicationBuilder AddApplicationLifecycle(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var lifecycleSection = builder.Configuration.GetSection(LifecycleOptions.SectionName);
        services.AddOptions<LifecycleOptions>()
            .Bind(lifecycleSection)
            .Validate(HasPositiveShutdownTimeout, "Lifecycle:ShutdownTimeoutSeconds must be greater than zero")
            .Validate(HasValidIdleShutdown, "Lifecycle:IdleShutdownMinutes must not be negative and Lifecycle:IdleCheckIntervalMilliseconds must be greater than zero")
            .ValidateOnStart();
        services.AddOptions<HostOptions>().Configure<IOptions<LifecycleOptions>>(ApplyShutdownTimeout);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<RealtimeChannel>();
        services.AddSingleton<IBrowserLauncher, SystemBrowserLauncher>();
        services.AddSingleton<BootstrapUrlFactory>();
        services.AddSingleton<WorkAdmissionGate>();
        services.AddSingleton<IShutdownStep, WorkAdmissionShutdownStep>();
        services.AddSingleton<GracefulShutdown>();
        services.AddSingleton<ActiveTransferWatcher>();
        services.AddSingleton<ApplicationExitCoordinator>();
        services.AddSingleton<ApplicationExitHandler>();
        services.AddSingleton<IBrowserPresence, RealtimeBrowserPresence>();
        services.AddScoped<UnsavedFileReporter>();
        services.AddScoped<PendingAttentionReporter>();
        services.AddHostedService<BrowserOpeningService>();
        services.AddHostedService<GracefulShutdownHostedService>();
        services.AddHostedService<IdleShutdownService>();
        return builder;
    }

    public static WebApplication UseApplicationLifecycle(this WebApplication app)
    {
        app.UseMiddleware<WorkAdmissionMiddleware>();
        return app;
    }

    public static IEndpointRouteBuilder MapApplicationLifecycle(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(ExitPath, RequestExit);
        endpoints.MapDelete(ExitPath, CancelExit);
        endpoints.MapGet(AttentionPath, GetPendingAttention);
        return endpoints;
    }

    private static IResult RequestExit(HttpContext context, [FromBody] ExitRequest? request, ApplicationExitHandler handler)
    {
        return handler.RequestExit(context, request);
    }

    private static IResult CancelExit(ApplicationExitHandler handler)
    {
        return handler.CancelExit();
    }

    private static async Task<IResult> GetPendingAttention(PendingAttentionReporter reporter, ApplicationExitCoordinator coordinator, CancellationToken cancellationToken)
    {
        var state = coordinator.State;
        var report = await reporter.ReportAsync(state, cancellationToken);
        return Results.Ok(report);
    }

    private static bool HasPositiveShutdownTimeout(LifecycleOptions options)
    {
        return options.ShutdownTimeoutSeconds > 0;
    }

    private static bool HasValidIdleShutdown(LifecycleOptions options)
    {
        return options.IdleShutdownMinutes >= 0 && options.IdleCheckIntervalMilliseconds > 0;
    }

    private static void ApplyShutdownTimeout(HostOptions hostOptions, IOptions<LifecycleOptions> lifecycleOptions)
    {
        var shutdownTimeoutSeconds = lifecycleOptions.Value.ShutdownTimeoutSeconds;
        hostOptions.ShutdownTimeout = TimeSpan.FromSeconds(shutdownTimeoutSeconds);
    }
}
