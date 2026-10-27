using DTech.Core.Connections;
using DTech.Core.SavedConnections;
using DTech.Core.SshConfig;
using DTech.Core.Terminal;
using DTech.Entrypoint.SavedConnections;
using DTech.Persistence.SavedConnections;
using DTech.Ssh;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class SavedConnectionsServiceCollectionExtensions
{
    public static IServiceCollection AddSavedConnections(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IHostKeyVerifier, UntrustedHostKeyVerifier>();
        AddSshTransportOnce(services);
        services.AddSingleton(SshConfigEnvironment.ForCurrentUser());
        services.AddSingleton<SshConfigParser>();
        services.AddSingleton(CreateImportOptions);
        services.AddSingleton(ConnectionTestOptions.Default);
        services.AddScoped<ISavedConnectionRepository, SavedConnectionRepository>();
        services.AddScoped<ConnectionPasswordStore>();
        services.AddScoped<ConnectionTargetFactory>();
        services.AddScoped<SavedConnectionService>();
        services.AddScoped<ConnectionTester>();
        services.AddScoped<SshConfigImporter>();
        return services;
    }

    private static SshConfigImportOptions CreateImportOptions(IServiceProvider serviceProvider)
    {
        var environment = serviceProvider.GetRequiredService<SshConfigEnvironment>();
        return new SshConfigImportOptions(environment.DefaultConfigPath);
    }

    private static void AddSshTransportOnce(IServiceCollection services)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(ITerminalSessionFactory)))
        {
            return;
        }

        services.AddSshTransport();
    }
}
