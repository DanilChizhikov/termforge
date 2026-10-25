using System.Net;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.Security;

[TestClass]
public sealed class OriginValidationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("http://attacker.example")]
    [DataRow("http://localhost:8080")]
    [DataRow("https://localhost")]
    [DataRow("null")]
    public async Task Http_request_with_foreign_origin_is_rejected_even_with_session(string origin)
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);

        using var response = await TestRequest.Get(SessionBootstrapper.SessionPath)
            .WithOrigin(origin)
            .WithSession(session)
            .SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task Bootstrap_exchange_with_foreign_origin_is_rejected_and_keeps_token_unused()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();
        var exchangeBody = new { token = factory.BootstrapTokenValue };

        using var foreignResponse = await TestRequest.Post(SessionBootstrapper.ExchangePath)
            .WithOrigin("http://attacker.example")
            .WithJson(exchangeBody)
            .SendAsync(client, TestContext.CancellationToken);
        using var sameOriginResponse = await SessionBootstrapper.ExchangeAsync(client, factory.BootstrapTokenValue, TestContext.CancellationToken);

        foreignResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        sameOriginResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [TestMethod]
    public async Task Http_request_with_same_origin_and_session_is_accepted()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);

        using var response = await TestRequest.Get(SessionBootstrapper.SessionPath)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithFetchSite("same-origin")
            .WithSession(session)
            .SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestMethod]
    [DataRow("cross-site")]
    [DataRow("same-site")]
    public async Task Request_marked_by_browser_as_cross_origin_is_rejected(string fetchSite)
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);

        using var response = await TestRequest.Get(SessionBootstrapper.SessionPath)
            .WithFetchSite(fetchSite)
            .WithSession(session)
            .SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task Navigation_typed_by_user_is_accepted()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();

        using var response = await TestRequest.Get("/bootstrap")
            .WithFetchSite("none")
            .SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
