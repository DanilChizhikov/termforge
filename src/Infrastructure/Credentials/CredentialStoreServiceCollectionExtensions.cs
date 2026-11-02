using DTech.Core.Credentials;
using DTech.Core.SettingsTransfer;
using DTech.Credentials;
using DTech.Credentials.Native;
using DTech.Credentials.Native.Processes;
using DTech.Credentials.Selection;
using DTech.Credentials.Vault;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class CredentialStoreServiceCollectionExtensions
{
    public static IServiceCollection AddCredentialStore(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(CredentialStoreOptions.SectionName);
        services.AddOptions<CredentialStoreOptions>().Bind(section)
            .Validate(options => options.VaultKdfIterations is >= CredentialStoreOptions.DefaultVaultKdfIterations and <= SettingsTransferLimits.MaxKdfIterations,
                "Credentials:VaultKdfIterations must be between 600000 and 10000000.")
            .Validate(options => options.ProbeTimeout > TimeSpan.Zero && options.ProbeTimeout <= CredentialStoreOptions.MaximumNativeTimeout,
                "Credentials:ProbeTimeout must be positive and no greater than one minute.")
            .Validate(options => options.OperationTimeout > TimeSpan.Zero && options.OperationTimeout <= CredentialStoreOptions.MaximumNativeTimeout,
                "Credentials:OperationTimeout must be positive and no greater than one minute.")
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ICredentialHelperProcessFactory, CredentialHelperProcessFactory>();
        services.AddSingleton<CredentialHelperRunner>();
        services.AddSingleton(CreateOperatingSystemStore);
        services.AddSingleton<OperatingSystemCredentialStoreProbe>();
        services.AddSingleton<VaultFile>();
        services.AddSingleton<EncryptedVault>();
        services.AddSingleton<CredentialBackendSelector>();
        services.AddSingleton<CredentialMutationCoordinator>();
        services.AddSingleton<IUncoordinatedCredentialStore, CredentialBackendStore>();
        services.AddSingleton<CoordinatedCredentialStore>();
        services.AddSingleton<ICredentialStore>(CreateCredentialStore);
        services.AddSingleton<ICredentialVault, CredentialVaultGate>();
        services.AddSingleton<ICredentialStoreStatusProvider, CredentialStoreStatusProvider>();
        return services;
    }

    private static CredentialStore CreateCredentialStore(IServiceProvider services)
    {
        var store = services.GetRequiredService<CoordinatedCredentialStore>();
        return new CredentialStore(store);
    }

    private static IOperatingSystemCredentialStore CreateOperatingSystemStore(IServiceProvider serviceProvider)
    {
        var store = OperatingSystemCredentialStoreFactory.Create();
        var runner = serviceProvider.GetRequiredService<CredentialHelperRunner>();
        return new ProcessCredentialStore(store, runner);
    }
}
