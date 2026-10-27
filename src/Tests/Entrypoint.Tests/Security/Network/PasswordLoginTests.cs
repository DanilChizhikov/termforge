using System.Net;
using System.Net.Http.Json;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.Security.Network;

[TestClass]
public sealed class PasswordLoginTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Password_login_is_disabled_by_default()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();

        using var statusResponse = await TestRequest.Get(NetworkAccessSettings.LoginPath).SendAsync(client, TestContext.CancellationToken);
        var status = await statusResponse.Content.ReadFromJsonAsync<LoginStatusBody>(TestContext.CancellationToken);
        using var loginResponse = await SendLoginAsync(client, TermForgeApplicationFactory.ApplicationOrigin, NetworkAccessSettings.Password);

        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        status!.PasswordLoginEnabled.Should().BeFalse();
        status.SignedInWithPassword.Should().BeFalse();
        loginResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        loginResponse.Headers.Contains("Set-Cookie").Should().BeFalse();
    }

    [TestMethod]
    public async Task Allowed_network_host_is_rejected_by_default()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientFor(NetworkAccessSettings.NetworkBaseAddress);

        using var response = await TestRequest.Get("/").SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task Password_login_status_is_public_in_network_mode()
    {
        await using var factory = CreateNetworkFactory();
        using var client = factory.CreateClientFor(NetworkAccessSettings.NetworkBaseAddress);

        using var response = await TestRequest.Get(NetworkAccessSettings.LoginPath).SendAsync(client, TestContext.CancellationToken);
        var status = await response.Content.ReadFromJsonAsync<LoginStatusBody>(TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        status!.PasswordLoginEnabled.Should().BeTrue();
    }

    [TestMethod]
    public async Task Correct_password_creates_secure_session_cookie()
    {
        await using var factory = CreateNetworkFactory();
        using var client = factory.CreateClientFor(NetworkAccessSettings.NetworkBaseAddress);

        using var response = await SendLoginAsync(client, NetworkAccessSettings.NetworkOrigin, NetworkAccessSettings.Password);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var setCookieHeader = SessionBootstrapper.ReadSetCookieHeader(response);
        setCookieHeader.Should().ContainEquivalentOf("httponly");
        setCookieHeader.Should().ContainEquivalentOf("samesite=strict");
        setCookieHeader.Should().ContainEquivalentOf("secure");
        setCookieHeader.Should().ContainEquivalentOf("path=/");
    }

    [TestMethod]
    public async Task Password_session_grants_api_access_with_csrf_token()
    {
        await using var factory = CreateNetworkFactory();
        using var client = factory.CreateClientFor(NetworkAccessSettings.NetworkBaseAddress);
        var session = await PasswordSessions.SignInAsync(client, TestContext.CancellationToken);

        using var statusResponse = await TestRequest.Get(NetworkAccessSettings.LoginPath)
            .WithSession(session)
            .SendAsync(client, TestContext.CancellationToken);
        var status = await statusResponse.Content.ReadFromJsonAsync<LoginStatusBody>(TestContext.CancellationToken);
        using var connectionsResponse = await TestRequest.Get("/api/connections")
            .WithSession(session)
            .SendAsync(client, TestContext.CancellationToken);

        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        status!.SignedInWithPassword.Should().BeTrue();
        connectionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestMethod]
    [DataRow("wrong password")]
    [DataRow("")]
    public async Task Wrong_password_is_rejected_without_session(string password)
    {
        await using var factory = CreateNetworkFactory();
        using var client = factory.CreateClientFor(NetworkAccessSettings.NetworkBaseAddress);

        using var response = await SendLoginAsync(client, NetworkAccessSettings.NetworkOrigin, password);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Contains("Set-Cookie").Should().BeFalse();
    }

    [TestMethod]
    public async Task Repeated_failures_lock_out_even_the_correct_password()
    {
        var settings = NetworkAccessSettings.CreateEnabled();
        settings["NetworkAccess:MaxFailedLoginAttempts"] = "2";
        await using var factory = new TermForgeApplicationFactory(settings);
        using var client = factory.CreateClientFor(NetworkAccessSettings.NetworkBaseAddress);

        using var firstFailure = await SendLoginAsync(client, NetworkAccessSettings.NetworkOrigin, "wrong password");
        using var secondFailure = await SendLoginAsync(client, NetworkAccessSettings.NetworkOrigin, "wrong password");
        using var lockedOut = await SendLoginAsync(client, NetworkAccessSettings.NetworkOrigin, NetworkAccessSettings.Password);

        firstFailure.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        secondFailure.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        lockedOut.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        lockedOut.Headers.RetryAfter.Should().NotBeNull();
        lockedOut.Headers.Contains("Set-Cookie").Should().BeFalse();
    }

    [TestMethod]
    public async Task Login_from_foreign_origin_is_rejected()
    {
        await using var factory = CreateNetworkFactory();
        using var client = factory.CreateClientFor(NetworkAccessSettings.NetworkBaseAddress);

        using var response = await SendLoginAsync(client, "https://attacker.example", NetworkAccessSettings.Password);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Headers.Contains("Set-Cookie").Should().BeFalse();
    }

    [TestMethod]
    public async Task Logout_invalidates_the_session()
    {
        await using var factory = CreateNetworkFactory();
        using var client = factory.CreateClientFor(NetworkAccessSettings.NetworkBaseAddress);
        var session = await PasswordSessions.SignInAsync(client, TestContext.CancellationToken);

        using var logoutResponse = await TestRequest.Post(NetworkAccessSettings.LogoutPath)
            .WithOrigin(NetworkAccessSettings.NetworkOrigin)
            .WithSession(session)
            .WithCsrfToken(session.CsrfToken)
            .SendAsync(client, TestContext.CancellationToken);
        using var sessionResponse = await TestRequest.Get(SessionBootstrapper.SessionPath)
            .WithSession(session)
            .SendAsync(client, TestContext.CancellationToken);

        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        sessionResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Logout_requires_csrf_token()
    {
        await using var factory = CreateNetworkFactory();
        using var client = factory.CreateClientFor(NetworkAccessSettings.NetworkBaseAddress);
        var session = await PasswordSessions.SignInAsync(client, TestContext.CancellationToken);

        using var logoutResponse = await TestRequest.Post(NetworkAccessSettings.LogoutPath)
            .WithOrigin(NetworkAccessSettings.NetworkOrigin)
            .WithSession(session)
            .SendAsync(client, TestContext.CancellationToken);

        logoutResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task Password_is_never_logged()
    {
        await using var factory = CreateNetworkFactory();
        using var client = factory.CreateClientFor(NetworkAccessSettings.NetworkBaseAddress);

        using var failure = await SendLoginAsync(client, NetworkAccessSettings.NetworkOrigin, "wrong password value");
        var session = await PasswordSessions.SignInAsync(client, TestContext.CancellationToken);

        factory.Logs.Messages.Should().NotContain(message => message.Contains(NetworkAccessSettings.Password));
        factory.Logs.Messages.Should().NotContain(message => message.Contains("wrong password value"));
        factory.Logs.Messages.Should().NotContain(message => message.Contains(session.SessionToken));
        factory.Logs.Messages.Should().NotContain(message => message.Contains(NetworkAccessSettings.PasswordHash));
    }

    private static TermForgeApplicationFactory CreateNetworkFactory()
    {
        var settings = NetworkAccessSettings.CreateEnabled();
        return new TermForgeApplicationFactory(settings);
    }

    private Task<HttpResponseMessage> SendLoginAsync(HttpClient client, string origin, string password)
    {
        var attempt = new LoginAttempt(origin, password);
        return PasswordSessions.SendLoginAsync(client, attempt, TestContext.CancellationToken);
    }

    private sealed record LoginStatusBody(bool PasswordLoginEnabled, bool SignedInWithPassword);
}
