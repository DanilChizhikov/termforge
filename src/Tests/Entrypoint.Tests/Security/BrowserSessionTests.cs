using System.Net;
using System.Net.Http.Json;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.Security;

[TestClass]
public sealed class BrowserSessionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("/api/session")]
    [DataRow("/api/anything")]
    public async Task Api_request_without_session_is_rejected(string path)
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();

        using var response = await TestRequest.Get(path).SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Api_request_with_forged_session_is_rejected()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();
        await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);

        using var response = await TestRequest.Get(SessionBootstrapper.SessionPath)
            .WithCookie("termforge_session=forged")
            .SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Api_request_with_bootstrap_token_as_session_is_rejected()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();

        using var response = await TestRequest.Get(SessionBootstrapper.SessionPath)
            .WithCookie($"termforge_session={factory.BootstrapTokenValue}")
            .SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Bootstrap_token_exchange_sets_http_only_strict_session_cookie()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();

        using var response = await SessionBootstrapper.ExchangeAsync(client, factory.BootstrapTokenValue, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var setCookieHeader = SessionBootstrapper.ReadSetCookieHeader(response);
        setCookieHeader.Should().ContainEquivalentOf("httponly");
        setCookieHeader.Should().ContainEquivalentOf("samesite=strict");
        setCookieHeader.Should().ContainEquivalentOf("path=/");
        setCookieHeader.Should().NotContain(factory.BootstrapTokenValue);
    }

    [TestMethod]
    public async Task Bootstrap_token_exchange_works_only_once()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();

        using var firstResponse = await SessionBootstrapper.ExchangeAsync(client, factory.BootstrapTokenValue, TestContext.CancellationToken);
        using var secondResponse = await SessionBootstrapper.ExchangeAsync(client, factory.BootstrapTokenValue, TestContext.CancellationToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        secondResponse.Headers.Contains("Set-Cookie").Should().BeFalse();
    }

    [TestMethod]
    public async Task Concurrent_bootstrap_token_exchanges_create_a_single_session()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();
        var exchanges = new List<Task<HttpResponseMessage>>();
        for (var i = 0; i < 8; i++)
        {
            var exchange = SessionBootstrapper.ExchangeAsync(client, factory.BootstrapTokenValue, TestContext.CancellationToken);
            exchanges.Add(exchange);
        }

        var responses = await Task.WhenAll(exchanges);

        responses.Count(response => response.StatusCode == HttpStatusCode.NoContent).Should().Be(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Forbidden).Should().Be(7);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("guessed-token")]
    public async Task Invalid_bootstrap_token_is_rejected_and_keeps_real_token_usable(string? token)
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();

        using var invalidResponse = await SessionBootstrapper.ExchangeAsync(client, token, TestContext.CancellationToken);
        using var validResponse = await SessionBootstrapper.ExchangeAsync(client, factory.BootstrapTokenValue, TestContext.CancellationToken);

        invalidResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        validResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [TestMethod]
    public async Task Session_endpoint_returns_csrf_token_for_valid_session()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);

        using var response = await TestRequest.Get(SessionBootstrapper.SessionPath)
            .WithSession(session)
            .SendAsync(client, TestContext.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        body.Should().ContainKey("csrfToken").WhoseValue.Should().Be(session.CsrfToken);
        session.CsrfToken.Should().NotBe(session.SessionToken);
    }

    [TestMethod]
    public async Task Bootstrap_page_is_served_without_session_and_restricts_scripts()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();

        using var pageResponse = await TestRequest.Get("/bootstrap").SendAsync(client, TestContext.CancellationToken);
        using var scriptResponse = await TestRequest.Get("/bootstrap.js").SendAsync(client, TestContext.CancellationToken);
        var page = await pageResponse.Content.ReadAsStringAsync(TestContext.CancellationToken);
        var script = await scriptResponse.Content.ReadAsStringAsync(TestContext.CancellationToken);

        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        pageResponse.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("script-src 'self'");
        pageResponse.Headers.GetValues("X-Frame-Options").Single().Should().Be("DENY");
        page.Should().Contain("/bootstrap.js");
        scriptResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        script.Should().Contain("/api/session/bootstrap");
    }
}
