using Microsoft.Extensions.Options;

namespace DTech.Persistence.Database;

public sealed class DatabasePathResolver
{
    public const string ApplicationDirectoryName = "termforge";
    public const string DatabaseFileName = "termforge.db";

    private readonly IOptions<PersistenceOptions> _options;

    public DatabasePathResolver(IOptions<PersistenceOptions> options)
    {
        _options = options;
    }

    public string Resolve()
    {
        var configuredPath = _options.Value.DatabasePath;
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        return ResolveDefault();
    }

    public static string ResolveDefault()
    {
        var userDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrEmpty(userDataDirectory))
        {
            userDataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
        }

        return Path.Combine(userDataDirectory, ApplicationDirectoryName, DatabaseFileName);
    }
}
