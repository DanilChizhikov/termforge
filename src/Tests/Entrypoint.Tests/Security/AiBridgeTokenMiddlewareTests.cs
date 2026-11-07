using System.Net;
using DTech.Core.AiBridge.Settings;
using DTech.Entrypoint.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DTech.Entrypoint.Tests.Security;

[TestClass]
public sealed class AiBridgeTokenMiddlewareTests
{
    private const string Token = "valid-ai-bridge-token";

    [TestMethod]
    [DataRow("termforge.lan", "127.0.0.1")]
    [DataRow("termforge.lan:5023", "::1")]
    [DataRow("localhost.example", "127.0.0.1")]
    [DataRow("192.168.1.20", "127.0.0.1")]
    [DataRow("localhost", "192.168.1.20")]
    [DataRow("", "127.0.0.1")]
    public async Task Nonlocal_host_or_client_is_rejected_with_a_valid_token(string host, string remoteAddress)
    {
        using var services = CreateServices();
        var context = CreateContext(host, remoteAddress, services);
        var called = false;
        var middleware = new AiBridgeTokenMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        }, NullLogger<AiBridgeTokenMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        called.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("localhost", "127.0.0.1")]
    [DataRow("LOCALHOST:5023", "127.0.0.1")]
    [DataRow("127.0.0.1:5023", "127.0.0.1")]
    [DataRow("[::1]:5023", "::1")]
    public async Task Loopback_host_and_client_are_accepted_with_a_valid_token(string host, string remoteAddress)
    {
        using var services = CreateServices();
        var context = CreateContext(host, remoteAddress, services);
        var called = false;
        var middleware = new AiBridgeTokenMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        }, NullLogger<AiBridgeTokenMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        called.Should().BeTrue();
    }

    private static DefaultHttpContext CreateContext(string host, string remoteAddress, IServiceProvider services)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = "/mcp";
        context.Request.Host = new HostString(host);
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
        context.Request.Headers.Authorization = $"Bearer {Token}";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static ServiceProvider CreateServices()
    {
        var store = Substitute.For<IAiBridgeSettingsStore>();
        store.LoadAsync(Arg.Any<CancellationToken>()).Returns(new AiBridgeSettings(true, true));
        var generator = Substitute.For<IAiBridgeTokenGenerator>();
        var repository = Substitute.For<IAiBridgeTokenRepository>();
        var hash = AiBridgeTokenHasher.Hash(Token);
        repository.ListHashesAsync(Arg.Any<CancellationToken>()).Returns(new[] { hash });
        var settings = new AiBridgeSettingsService(store);
        var tokens = new AiBridgeTokenService(repository, generator, TimeProvider.System);
        return new ServiceCollection()
            .AddSingleton(settings)
            .AddSingleton(tokens)
            .BuildServiceProvider();
    }
}
