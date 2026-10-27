using DTech.Core.SettingsTransfer.Export;
using DTech.Core.SettingsTransfer.Import;
using DTech.Core.SettingsTransfer.Secrets;
using DTech.Credentials.Export;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class SettingsTransferServiceCollectionExtensions
{
    public static IServiceCollection AddSettingsTransfer(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ISecretsArchiveCipher, SecretsArchiveCipher>();
        services.AddScoped<PinnedCommandReader>();
        services.AddScoped<SettingsTransferSummaryReader>();
        services.AddScoped<SettingsDocumentBuilder>();
        services.AddScoped<SecretsArchiveBuilder>();
        services.AddScoped<SettingsExportService>();
        services.AddScoped<SettingsImportLoader>();
        services.AddScoped<PreferencesImportPlanner>();
        services.AddScoped<SettingsImportPreparer>();
        services.AddScoped<ConnectionSecretWriter>();
        services.AddScoped<ConnectionImportApplier>();
        services.AddScoped<PreferencesImportApplier>();
        services.AddScoped<WorkspaceImportApplier>();
        services.AddScoped<PinnedCommandImportApplier>();
        services.AddScoped<SettingsImportExecutor>();
        services.AddScoped<SettingsImportService>();
        return services;
    }
}
