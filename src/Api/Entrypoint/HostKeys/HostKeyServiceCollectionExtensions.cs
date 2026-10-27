using DTech.Entrypoint.HostKeys;
using DTech.Entrypoint.Realtime;
using DTech.HostKeys.Decisions;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class HostKeyServiceCollectionExtensions
{
    public static IServiceCollection AddHostKeys(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton<RealtimeChannel>();
        services.AddSingleton<IPendingHostKeyDecisionNotifier, HostKeyBroadcaster>();
        services.AddHostKeyVerification(configuration);
        return services;
    }
}
