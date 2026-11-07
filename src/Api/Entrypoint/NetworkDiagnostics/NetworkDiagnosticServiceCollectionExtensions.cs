using DTech.Entrypoint.NetworkDiagnostics;

namespace Microsoft.Extensions.DependencyInjection;

public static class NetworkDiagnosticServiceCollectionExtensions
{
    public static IServiceCollection AddNetworkDiagnostics(this IServiceCollection services)
    {
        services.AddSingleton<NetworkDiagnosticStore>();
        services.AddSingleton<NetworkProbeLimiter>();
        services.AddSingleton<RemoteNetworkActionPlans>();
        services.AddScoped<NetworkProbeSessionFactory>();
        services.AddScoped<NetworkDiagnosticService>();
        services.AddScoped<RemoteNetworkActionService>();
        services.AddHostedService<NetworkDiagnosticWorker>();
        return services;
    }
}
