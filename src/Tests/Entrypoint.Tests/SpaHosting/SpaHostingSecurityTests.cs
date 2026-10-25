using System.Net;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.SpaHosting;

[TestClass]
public sealed class SpaHostingSecurityTests
{
    private const string IndexContent = "<html>spa-shell</html>";
    private const string AssetContent = "console.log('asset');";

    private string _webRoot = string.Empty;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _webRoot = Path.Combine(Path.GetTempPath(), $"termforge-spa-sec-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), IndexContent);
        File.WriteAllText(Path.Combine(_webRoot, "assets", "app.js"), AssetContent);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Directory.Delete(_webRoot, true);
    }

    [TestMethod]
    [DataRow("/")]
    [DataRow("/servers/1/terminal")]
    public async Task Shell_is_served_without_session(string path)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClientWithoutCookies();

        using var response = await TestRequest.Get(path).SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        body.Should().Be(IndexContent);
    }

    [TestMethod]
    public async Task Static_asset_is_served_without_session()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClientWithoutCookies();

        using var response = await TestRequest.Get("/assets/app.js").SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        body.Should().Be(AssetContent);
    }

    [TestMethod]
    [DataRow("/")]
    [DataRow("/assets/app.js")]
    [DataRow("/servers/1/terminal")]
    public async Task Static_content_with_foreign_host_is_rejected(string path)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClientFor("http://attacker.example/");

        using var response = await TestRequest.Get(path).SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task Unknown_api_path_without_session_is_unauthorized_not_shell()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClientWithoutCookies();

        using var response = await TestRequest.Get("/api/unknown").SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        body.Should().NotBe(IndexContent);
    }

    [TestMethod]
    public async Task Unknown_api_path_with_session_is_not_found_not_shell()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);

        using var response = await TestRequest.Get("/api/unknown").WithSession(session).SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        body.Should().NotBe(IndexContent);
    }

    [TestMethod]
    public async Task Session_endpoint_still_works_next_to_the_shell()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClientWithoutCookies();

        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);

        session.CsrfToken.Should().NotBeNullOrEmpty();
    }

    private TermForgeApplicationFactory CreateFactory()
    {
        return new TermForgeApplicationFactory { WebRootPath = _webRoot };
    }
}
