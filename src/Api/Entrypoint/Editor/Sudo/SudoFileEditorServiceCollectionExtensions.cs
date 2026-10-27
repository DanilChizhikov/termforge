using DTech.Core.Editor.Sudo;
using DTech.Ssh;

namespace Microsoft.Extensions.DependencyInjection;

public static class SudoFileEditorServiceCollectionExtensions
{
    public static IServiceCollection AddSudoFileEditor(this IServiceCollection services)
    {
        services.AddRemoteShellTransport();
        services.AddSingleton<SudoCommandBuilder>();
        services.AddSingleton<SudoResponseClassifier>();
        services.AddSingleton<SudoShellFactory>();
        services.AddSingleton<SudoAuthenticator>();
        services.AddSingleton<SudoAvailabilityProbe>();
        services.AddSingleton<FileAccessCommands>();
        services.AddScoped<RemoteShellConnector>();
        services.AddScoped<SudoPasswordStore>();
        services.AddScoped<SudoPasswordResolver>();
        services.AddScoped<ISudoEditorSessionOpener, SudoEditorSessionOpener>();
        services.AddScoped<ISudoStatusReader, SudoStatusReader>();
        services.AddScoped<IFileAccessReader, FileAccessReader>();
        services.AddScoped<SudoFileEditor>();
        services.AddScoped<SudoFileSaveService>();
        return services;
    }
}
