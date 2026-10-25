using DTech.Entrypoint.Security;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.Security;

[TestClass]
public sealed class LoopbackBindingStartupTests
{
    [TestMethod]
    [DataRow("urls", "http://0.0.0.0:5023")]
    [DataRow("Kestrel:Endpoints:Http:Url", "http://192.168.1.10:5023")]
    [DataRow("http_ports", "5023")]
    public async Task Host_refuses_to_start_with_non_loopback_binding(string key, string value)
    {
        var settings = new Dictionary<string, string?> { [key] = value };
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
}
