using DTech.Core.Files.Local;
using DTech.Core.Files.Remote;

namespace Microsoft.Extensions.DependencyInjection;

public static class FileBrowsingServiceCollectionExtensions
{
    public static IServiceCollection AddFileBrowsing(this IServiceCollection services)
    {
        services.AddSingleton<LocalPathResolver>();
        services.AddSingleton<LocalRootsProvider>();
        services.AddSingleton<LocalEntryFactory>();
        services.AddSingleton<LocalFileBrowser>();
        services.AddSingleton<LocalFileOperations>();
        services.AddSingleton<RemotePathNormalizer>();
        services.AddScoped<IRemoteConnectionOpener, RemoteConnectionOpener>();
        services.AddScoped<RemoteFileBrowser>();
        services.AddScoped<RemoteFileOperations>();
        return services;
    }
}
