using DTech.Entrypoint.SpaHosting;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.SpaHosting;

[TestClass]
public sealed class PublishedContentRootTests
{
    private string _applicationDirectory = null!;

    [TestInitialize]
    public void CreateApplicationDirectory()
    {
        _applicationDirectory = Path.Combine(Path.GetTempPath(), $"termforge-content-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_applicationDirectory);
    }

    [TestCleanup]
    public void DeleteApplicationDirectory()
    {
        Directory.Delete(_applicationDirectory, true);
    }

    [TestMethod]
    public void Find_returns_the_application_directory_when_it_contains_the_bundled_frontend()
    {
        var webRoot = Path.Combine(_applicationDirectory, "wwwroot");
        Directory.CreateDirectory(webRoot);
        var indexPath = Path.Combine(webRoot, "index.html");
        File.WriteAllText(indexPath, "<div id=\"root\"></div>");

        var contentRoot = PublishedContentRoot.Find(_applicationDirectory);

        contentRoot.Should().Be(_applicationDirectory);
    }

    [TestMethod]
    public void Find_returns_null_when_the_application_directory_has_no_web_root()
    {
        var contentRoot = PublishedContentRoot.Find(_applicationDirectory);

        contentRoot.Should().BeNull();
    }

    [TestMethod]
    public void Find_returns_null_when_the_web_root_has_no_index_file()
    {
        var webRoot = Path.Combine(_applicationDirectory, "wwwroot");
        Directory.CreateDirectory(webRoot);

        var contentRoot = PublishedContentRoot.Find(_applicationDirectory);

        contentRoot.Should().BeNull();
    }
}
