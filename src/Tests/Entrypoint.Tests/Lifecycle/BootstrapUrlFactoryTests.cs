using DTech.Entrypoint.Lifecycle;
using DTech.Entrypoint.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using NSubstitute;
using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.Tests.Lifecycle;

[TestClass]
public sealed class BootstrapUrlFactoryTests
{
    private readonly BootstrapToken _bootstrapToken = new(TimeProvider.System);

    [TestMethod]
    [DataRow("http://127.0.0.1:5023", "http://127.0.0.1:5023")]
    [DataRow("http://0.0.0.0:5023", "http://127.0.0.1:5023")]
    [DataRow("https://0.0.0.0:5443", "https://127.0.0.1:5443")]
    [DataRow("http://[::]:5023", "http://[::1]:5023")]
    public void Bootstrap_url_uses_a_loopback_origin(string serverAddress, string expectedOrigin)
    {
        var factory = CreateFactory(serverAddress);

        var bootstrapUrl = factory.Create();

        bootstrapUrl.Should().NotBeNull();
        bootstrapUrl!.AbsoluteUri.Should().Be($"{expectedOrigin}/bootstrap#token={_bootstrapToken.Value}");
    }

    [TestMethod]
    [DataRow("http://192.168.1.10:5023")]
    [DataRow("https://termforge.lan:5023")]
    public void Bootstrap_url_is_not_created_for_network_only_address(string serverAddress)
    {
        var factory = CreateFactory(serverAddress);

        var bootstrapUrl = factory.Create();

        bootstrapUrl.Should().BeNull();
    }

    [TestMethod]
    public void Configured_development_origin_keeps_the_proof_on_the_proxy_origin()
    {
        var factory = CreateFactory("http://127.0.0.1:5023", "http://127.0.0.1:5173");

        var bootstrapUrl = factory.Create();

        bootstrapUrl!.AbsoluteUri.Should().Be($"http://127.0.0.1:5173/bootstrap#token={_bootstrapToken.Value}");
    }

    [TestMethod]
    [DataRow("http://attacker.example")]
    [DataRow("file:///tmp/launch")]
    [DataRow("http://127.0.0.1:5173/other")]
    [DataRow("http://user@127.0.0.1:5173")]
    public void Configured_origin_cannot_send_the_launch_token_outside_loopback(string origin)
    {
        var factory = CreateFactory("http://127.0.0.1:5023", origin);

        var create = () => factory.Create();

        create.Should().Throw<InvalidOperationException>();
    }

    private BootstrapUrlFactory CreateFactory(string serverAddress, string? browserOrigin = null)
    {
        var serverAddresses = new ServerAddressesFeature();
        serverAddresses.Addresses.Add(serverAddress);
        var features = new FeatureCollection();
        features.Set<IServerAddressesFeature>(serverAddresses);
        var server = Substitute.For<IServer>();
        server.Features.Returns(features);
        var lifecycleOptions = new LifecycleOptions { BrowserOrigin = browserOrigin };
        var options = Options.Create(lifecycleOptions);
        return new BootstrapUrlFactory(server, _bootstrapToken, options);
    }
}
