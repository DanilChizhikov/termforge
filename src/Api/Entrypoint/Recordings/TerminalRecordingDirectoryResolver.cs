using DTech.Core.Terminal.Recording;
using DTech.Persistence.Database;
using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.Recordings;

internal static class TerminalRecordingDirectoryResolver
{
    public static TerminalRecordingStorageOptions Resolve(IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptions<TerminalRecordingOptions>>();
        var configuredDirectory = options.Value.Directory;
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            return new TerminalRecordingStorageOptions(configuredDirectory);
        }

        var databasePathResolver = services.GetRequiredService<DatabasePathResolver>();
        var databasePath = databasePathResolver.Resolve();
        var dataDirectory = Path.GetDirectoryName(databasePath) ?? AppContext.BaseDirectory;
        var recordingsDirectory = Path.Combine(dataDirectory, TerminalRecordingOptions.DefaultDirectoryName);
        return new TerminalRecordingStorageOptions(recordingsDirectory);
    }
}
