using DTech.Core.Terminal.Sessions;
using DTech.Entrypoint.Terminals;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class TerminalServiceCollectionExtensions
{
    public static IServiceCollection AddTerminals(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(TerminalSessionOptions.Default);
        services.AddSingleton<TerminalSessionManager>();
        services.AddScoped<TerminalLauncher>();
        services.AddSingleton<TerminalWebSocketBridge>();
        services.AddHostedService<TerminalShutdownService>();
        return services;
    }
}
