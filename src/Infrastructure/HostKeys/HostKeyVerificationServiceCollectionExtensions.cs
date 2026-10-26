using DTech.Core.Connections;
using DTech.HostKeys;
using DTech.HostKeys.Decisions;
using DTech.HostKeys.KnownHosts;
using DTech.HostKeys.Trust;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class HostKeyVerificationServiceCollectionExtensions
{
    public static IServiceCollection AddHostKeyVerification(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(HostKeyVerificationOptions.SectionName);
        services.AddOptions<HostKeyVerificationOptions>().Bind(section);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<KnownHostsFiles>();
        services.AddSingleton<KnownHostsReader>();
        services.AddSingleton<ITrustedHostKeyStore, TrustedHostKeyStore>();
        services.AddSingleton<IHostKeyVerifier, HostKeyVerifier>();
        services.TryAddSingleton<IPendingHostKeyDecisionNotifier, NullPendingHostKeyDecisionNotifier>();
        services.AddSingleton<HostKeyDecisionBroker>();
        services.AddSingleton<IHostKeyTrustPrompt>(GetBroker);
        services.AddSingleton<HostKeyDecisionHandler>();
        return services;
    }

    private static HostKeyDecisionBroker GetBroker(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<HostKeyDecisionBroker>();
    }
}
