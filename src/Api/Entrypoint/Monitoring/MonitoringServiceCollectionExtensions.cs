using DTech.Core.Monitoring;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class MonitoringServiceCollectionExtensions
{
    public static IServiceCollection AddServerMonitoring(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ServerMetricsCollector>();
        services.AddScoped<ServerMonitoringService>();
        return services;
    }
}
