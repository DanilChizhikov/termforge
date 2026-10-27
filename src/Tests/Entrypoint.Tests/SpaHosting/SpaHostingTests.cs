using System.Net;
using DTech.Entrypoint.SpaHosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DTech.Entrypoint.Tests.SpaHosting;

[TestClass]
public sealed class SpaHostingTests
{
    private const string IndexContent = "<html>spa-shell</html>";
    private const string AssetContent = "console.log('asset');";

    private string _webRoot = string.Empty;
    private WebApplication? _app;
    private HttpClient _client = null!;

    [TestInitialize]
    public void Initialize()
    {
        _webRoot = Path.Combine(Path.GetTempPath(), $"termforge-spa-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), IndexContent);
        File.WriteAllText(Path.Combine(_webRoot, "assets", "app.js"), AssetContent);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        _client?.Dispose();
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        Directory.Delete(_webRoot, true);
    }

    [TestMethod]
    public async Task Root_ReturnsIndex()
    {
        await StartAsync(_webRoot);

        var body = await _client.GetStringAsync("/");

        Assert.AreEqual(IndexContent, body);
    }

    [TestMethod]
    public async Task DeepLink_ReturnsIndex()
    {
        await StartAsync(_webRoot);

        var body = await _client.GetStringAsync("/servers/42/terminal");

        Assert.AreEqual(IndexContent, body);
    }

    [TestMethod]
    public async Task StaticAsset_IsServedAsIs()
    {
        await StartAsync(_webRoot);

        var body = await _client.GetStringAsync("/assets/app.js");

        Assert.AreEqual(AssetContent, body);
    }

    [TestMethod]
    [DataRow("/api/unknown")]
    [DataRow("/api")]
    [DataRow("/api/a/b/c")]
    public async Task ApiPath_IsNotSwallowedByFallback(string path)
    {
        await StartAsync(_webRoot);

        var response = await _client.GetAsync(path);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.AreNotEqual(IndexContent, body);
    }

    [TestMethod]
    public async Task ApiEndpoint_WinsOverFallback()
    {
        await StartAsync(_webRoot, application => application.MapGet("/api/ping", () => "pong"));

        var body = await _client.GetStringAsync("/api/ping");

        Assert.AreEqual("pong", body);
    }

    [TestMethod]
    public async Task WithoutBuiltFrontend_RootExplainsHowToBuild()
    {
        var emptyRoot = Path.Combine(_webRoot, "empty");
        Directory.CreateDirectory(emptyRoot);
        await StartAsync(emptyRoot);

        var body = await _client.GetStringAsync("/");

        StringAssert.Contains(body, "frontend is not built");
    }

    private async Task StartAsync(string webRoot, Action<WebApplication>? configure = null)
    {
        var options = new WebApplicationOptions { ContentRootPath = _webRoot, WebRootPath = webRoot };
        var builder = WebApplication.CreateBuilder(options);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        _app = builder.Build();
        configure?.Invoke(_app);
        _app.UseSpaHosting();
        await _app.StartAsync();

        var server = _app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.First();
        _client = new HttpClient { BaseAddress = new Uri(address) };
    }
}
