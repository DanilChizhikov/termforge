using DTech.Entrypoint.Lifecycle;
using DTech.Entrypoint.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using NSubstitute;

namespace DTech.Entrypoint.Tests.Lifecycle;

[TestClass]
public sealed class BootstrapUrlFactoryTests
{
    private readonly BootstrapToken _bootstrapToken = new();

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

    private BootstrapUrlFactory CreateFactory(string serverAddress)
    {
        var serverAddresses = new ServerAddressesFeature();
        serverAddresses.Addresses.Add(serverAddress);
        var features = new FeatureCollection();
        features.Set<IServerAddressesFeature>(serverAddresses);
        var server = Substitute.For<IServer>();
        server.Features.Returns(features);
        return new BootstrapUrlFactory(server, _bootstrapToken);
    }
}
