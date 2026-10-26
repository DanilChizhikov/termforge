using DTech.Core.Editor;
using DTech.Core.Files.Remote;
using DTech.Entrypoint.Editor;
using DTech.Ssh;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static class RemoteFileEditorServiceCollectionExtensions
{
    public static IServiceCollection AddRemoteFileEditor(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(EditorOptions.SectionName);
        services.AddOptions<EditorOptions>().Bind(section).ValidateOnStart();
        services.AddSingleton<IValidateOptions<EditorOptions>, EditorOptionsValidator>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<RemotePathNormalizer>();
        services.AddRemoteEditorTransport();
        services.AddSingleton(CreateLimits);
        services.AddSingleton<EditorPathResolver>();
        services.AddSingleton<TextContentCodec>();
        services.AddSingleton<AuxiliaryFileNames>();
        services.AddSingleton<RemoteFileSnapshotReader>();
        services.AddSingleton<RemoteDocumentReader>();
        services.AddSingleton<ConflictDetector>();
        services.AddSingleton<BackupCatalog>();
        services.AddSingleton<BackupWriter>();
        services.AddSingleton<BackupPruner>();
        services.AddSingleton<FileReplacer>();
        services.AddSingleton<RemoteFileSaver>();
        services.AddSingleton<RemoteFileRollback>();
        services.AddScoped<IRemoteEditorConnectionOpener, RemoteEditorConnectionOpener>();
        services.AddScoped<EditorOperationRecorder>();
        services.AddScoped<RemoteFileEditor>();
        services.AddScoped<RemoteFileSaveService>();
        return services;
    }

    private static EditorLimits CreateLimits(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<EditorOptions>>().Value;
        return new EditorLimits(options.MaxFileBytes, options.MaxBackupsPerFile);
    }
}
