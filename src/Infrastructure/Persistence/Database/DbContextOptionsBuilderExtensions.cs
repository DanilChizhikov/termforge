using Microsoft.EntityFrameworkCore;

namespace DTech.Persistence.Database;

public static class DbContextOptionsBuilderExtensions
{
    public const string MigrationsAssemblyName = "Persistence.Migrations";

    public static DbContextOptionsBuilder UseTermForgeSqlite(this DbContextOptionsBuilder builder, string databasePath)
    {
        var connectionString = $"Data Source={databasePath}";
        builder.UseSqlite(connectionString, sqlite => sqlite.MigrationsAssembly(MigrationsAssemblyName));
        builder.UseSnakeCaseNamingConvention();
        return builder;
    }
}
