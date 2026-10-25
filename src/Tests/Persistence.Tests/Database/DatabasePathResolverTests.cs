using DTech.Persistence.Database;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace DTech.Persistence.Tests.Database;

[TestClass]
public sealed class DatabasePathResolverTests
{
    [TestMethod]
    public void Resolve_ConfiguredPath_ReturnsFullConfiguredPath()
    {
        var configured = Path.Combine(Path.GetTempPath(), "custom", "app.db");
        var options = Options.Create(new PersistenceOptions { DatabasePath = configured });
        var resolver = new DatabasePathResolver(options);

        var path = resolver.Resolve();

        path.Should().Be(Path.GetFullPath(configured));
    }

    [TestMethod]
    public void Resolve_NothingConfigured_ReturnsFileInUserApplicationData()
    {
        var options = Options.Create(new PersistenceOptions());
        var resolver = new DatabasePathResolver(options);
        var userData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);

        var path = resolver.Resolve();

        path.Should().StartWith(userData);
        path.Should().EndWith(Path.Combine("termforge", "termforge.db"));
    }
}
