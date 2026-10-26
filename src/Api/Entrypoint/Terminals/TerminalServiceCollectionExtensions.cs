using DTech.Core.Reconnect;
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
        services.AddScoped<ITerminalLauncher>(provider => provider.GetRequiredService<TerminalLauncher>());
        services.AddSingleton<ReconnectScheduler>();
        services.AddScoped<ReconnectService>();
        services.AddSingleton<TerminalWebSocketBridge>();
        services.AddHostedService<TerminalShutdownService>();
        return services;
    }
}
