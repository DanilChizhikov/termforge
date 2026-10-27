using DTech.Core.Safety;
using DTech.Entrypoint.Safety;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class WriteSafetyServiceCollectionExtensions
{
    public static IServiceCollection AddWriteSafety(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IConfirmationTokenStore, ConfirmationTokenStore>();
        services.AddSingleton<IServerAccessModeReader, ScopedServerAccessModeReader>();
        services.AddSingleton<ServerWriteGate>();
        services.AddScoped<ConfirmationIssuer>();
        return services;
    }
}
