using DTech.Entrypoint.Security;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.Security;

[TestClass]
public sealed class LoopbackBindingStartupTests
{
    [TestMethod]
    [DataRow("Kestrel:Endpoints:Http:Url", "http://192.168.1.10:5023", "false")]
    [DataRow("urls", "http://0.0.0.0:5023", "true")]
    [DataRow("http_ports", "5023", "true")]
    public async Task Host_refuses_to_start_with_non_loopback_binding(string key, string value, string preferHostingUrls)
    {
        var settings = new Dictionary<string, string?>
        {
            [key] = value,
            ["preferHostingUrls"] = preferHostingUrls,
        };
        await using var factory = new TermForgeApplicationFactory(settings);

        var start = () => factory.CreateClient();

        start.Should().Throw<InsecureBindingException>().WithMessage("*Network access mode is not available*");
    }

    [TestMethod]
    public async Task Host_starts_with_default_loopback_binding()
    {
        await using var factory = new TermForgeApplicationFactory();

        var start = () => factory.CreateClient();

        start.Should().NotThrow();
    }

    [TestMethod]
    public async Task Host_starts_when_container_ports_are_overridden_by_loopback_endpoint()
    {
        var settings = new Dictionary<string, string?> { ["http_ports"] = "8080" };
        await using var factory = new TermForgeApplicationFactory(settings);

        var start = () => factory.CreateClient();

        start.Should().NotThrow();
    }
}
