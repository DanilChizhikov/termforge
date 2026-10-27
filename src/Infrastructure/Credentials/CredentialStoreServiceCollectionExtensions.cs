using DTech.Core.Credentials;
using DTech.Credentials;
using DTech.Credentials.Native;
using DTech.Credentials.Selection;
using DTech.Credentials.Vault;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static class CredentialStoreServiceCollectionExtensions
{
    public static IServiceCollection AddCredentialStore(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(CredentialStoreOptions.SectionName);
        services.AddOptions<CredentialStoreOptions>().Bind(section);
        services.AddSingleton(CreateOperatingSystemStore);
        services.AddSingleton<OperatingSystemCredentialStoreProbe>();
        services.AddSingleton<VaultFile>();
        services.AddSingleton<EncryptedVault>();
        services.AddSingleton<CredentialBackendSelector>();
        services.AddSingleton<ICredentialStore, CredentialStore>();
        services.AddSingleton<ICredentialVault, CredentialVaultGate>();
        services.AddSingleton<ICredentialStoreStatusProvider, CredentialStoreStatusProvider>();
        return services;
    }

    private static IOperatingSystemCredentialStore CreateOperatingSystemStore(IServiceProvider serviceProvider)
    {
        return OperatingSystemCredentialStoreFactory.Create();
    }
}
