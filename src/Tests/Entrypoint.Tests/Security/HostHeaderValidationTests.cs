using System.Net;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.Security;

[TestClass]
public sealed class HostHeaderValidationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("http://attacker.example/")]
    [DataRow("http://rebind.attacker.example:5023/")]
    [DataRow("http://0.0.0.0:5023/")]
    [DataRow("http://192.168.1.10:5023/")]
    [DataRow("http://app.localhost:5023/")]
    public async Task Request_with_non_loopback_host_is_rejected(string baseAddress)
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientFor(baseAddress);

        using var response = await TestRequest.Get("/").SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    [DataRow("http://attacker.example/")]
    [DataRow("http://rebind.attacker.example:5023/")]
    public async Task Api_request_with_valid_session_and_non_loopback_host_is_rejected(string baseAddress)
    {
        await using var factory = new TermForgeApplicationFactory();
        using var loopbackClient = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, loopbackClient, TestContext.CancellationToken);
        using var foreignHostClient = factory.CreateClientFor(baseAddress);

        using var response = await TestRequest.Get(SessionBootstrapper.SessionPath)
            .WithSession(session)
            .SendAsync(foreignHostClient, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    [DataRow("http://localhost/")]
    [DataRow("http://127.0.0.1:5023/")]
    [DataRow("http://[::1]:5023/")]
    public async Task Request_with_loopback_host_is_accepted(string baseAddress)
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientFor(baseAddress);

        using var response = await TestRequest.Get("/").SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
