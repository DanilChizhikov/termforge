using DTech.Core.Files.Search;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class FileSearchServiceCollectionExtensions
{
    public static IServiceCollection AddFileSearch(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<FileTreeSearcher>();
        services.AddSingleton<LocalFileTreeReader>();
        services.AddSingleton<LocalFileSearchService>();
        services.AddSingleton<RemoteFindAvailability>();
        services.AddScoped<IRemoteCommandRunnerOpener, RemoteCommandRunnerOpener>();
        services.AddScoped<RemoteFindLauncher>();
        services.AddScoped<RemoteFileSearchService>();
        return services;
    }
}
