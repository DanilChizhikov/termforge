using DTech.Entrypoint.Security;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;

namespace DTech.Entrypoint.Tests.Security;

[TestClass]
public sealed class LoopbackBindingStartupTests
{
    private const string KestrelEndpointUrlKey = "Kestrel:Endpoints:Http:Url";
    private const string LoopbackEndpoint = "http://127.0.0.1:5023";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("http://192.168.1.10:5023", null, null)]
    [DataRow(LoopbackEndpoint, "urls", "http://0.0.0.0:5023")]
    [DataRow(LoopbackEndpoint, "http_ports", "5023")]
    public async Task Host_refuses_to_start_with_non_loopback_binding(string endpointUrl, string? hostingKey, string? hostingValue)
    {
        var settings = new Dictionary<string, string?>
        {
            [KestrelEndpointUrlKey] = endpointUrl,
            ["preferHostingUrls"] = "true",
        };
        if (hostingKey is not null)
        {
            settings[hostingKey] = hostingValue;
        }

        await using var app = BuildSecuredApplication(settings);

        var start = () => app.StartAsync(TestContext.CancellationToken);

        await start.Should().ThrowAsync<InsecureBindingException>().WithMessage("*Network access mode is not available*");
    }

    [TestMethod]
    public async Task Host_starts_when_hosting_addresses_are_overridden_by_loopback_endpoint()
    {
        var settings = new Dictionary<string, string?>
        {
            [KestrelEndpointUrlKey] = LoopbackEndpoint,
            ["urls"] = "http://0.0.0.0:5023",
            ["http_ports"] = "8080",
        };
        await using var app = BuildSecuredApplication(settings);

        var start = () => app.StartAsync(TestContext.CancellationToken);

        await start.Should().NotThrowAsync();
        await app.StopAsync(TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task Application_starts_with_default_loopback_binding()
    {
        await using var factory = new TermForgeApplicationFactory();

        var start = () => factory.CreateClient();

        start.Should().NotThrow();
    }

    private static WebApplication BuildSecuredApplication(Dictionary<string, string?> settings)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.WebHost.UseTestServer();
        builder.AddLocalHostSecurity();
        var app = builder.Build();
        app.UseLocalHostSecurity();
        return app;
    }
}
