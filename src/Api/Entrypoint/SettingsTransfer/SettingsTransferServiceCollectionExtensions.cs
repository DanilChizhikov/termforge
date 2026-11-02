using DTech.Core.SettingsTransfer.Export;
using DTech.Core.SettingsTransfer.Import;
using DTech.Core.SettingsTransfer.Package;
using DTech.Core.SavedConnections;
using DTech.Credentials.Export;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class SettingsTransferServiceCollectionExtensions
{
    public static IServiceCollection AddSettingsTransfer(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ISettingsPackageCipher, SettingsPackageCipher>();
        services.AddSingleton<ExportKeyGenerator>();
        services.AddScoped<PinnedCommandReader>();
        services.AddScoped<SettingsTransferSummaryReader>();
        services.AddScoped<SettingsDocumentBuilder>();
        services.AddScoped<ConnectionSecretsCollector>();
        services.AddScoped<SettingsExportService>();
        services.AddScoped<SettingsImportLoader>();
        services.AddScoped<PreferencesImportPlanner>();
        services.AddScoped<SettingsImportPreparer>();
        services.AddScoped<ImportSecretTransaction>();
        services.AddScoped(CreateSecretWriter);
        services.AddScoped(CreateConnectionApplier);
        services.AddScoped<PreferencesImportApplier>();
        services.AddScoped<WorkspaceImportApplier>();
        services.AddScoped<PinnedCommandImportApplier>();
        services.AddScoped<SettingsImportExecutor>();
        services.AddScoped<SettingsImportCoordinator>();
        services.AddScoped<SettingsImportService>();
        return services;
    }

    private static ConnectionSecretWriter CreateSecretWriter(IServiceProvider services)
    {
        var secrets = services.GetRequiredService<ImportSecretTransaction>();
        return new ConnectionSecretWriter(secrets);
    }

    private static ConnectionImportApplier CreateConnectionApplier(IServiceProvider services)
    {
        var repository = services.GetRequiredService<ISavedConnectionRepository>();
        var writer = services.GetRequiredService<ConnectionSecretWriter>();
        var secrets = services.GetRequiredService<ImportSecretTransaction>();
        var cleaner = new ConnectionSecretCleaner(secrets);
        var time = services.GetRequiredService<TimeProvider>();
        return new ConnectionImportApplier(repository, writer, cleaner, time);
    }
}
