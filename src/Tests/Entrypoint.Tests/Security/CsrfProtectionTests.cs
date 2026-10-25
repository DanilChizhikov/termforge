using System.Net;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.Security;

[TestClass]
public sealed class CsrfProtectionTests
{
    private const string ExitPath = "/api/lifecycle/exit";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task State_changing_request_without_csrf_token_is_rejected()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);

        using var response = await TestRequest.Post(ExitPath)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithSession(session)
            .SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        factory.ApplicationStopping.IsCancellationRequested.Should().BeFalse();
    }

    [TestMethod]
    public async Task State_changing_request_with_wrong_csrf_token_is_rejected()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);

        using var response = await TestRequest.Post(ExitPath)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithSession(session)
            .WithCsrfToken(session.SessionToken)
            .SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        factory.ApplicationStopping.IsCancellationRequested.Should().BeFalse();
    }

    [TestMethod]
    public async Task State_changing_request_without_session_is_rejected()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();

        using var response = await TestRequest.Post(ExitPath)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithCsrfToken("guessed-token")
            .SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        factory.ApplicationStopping.IsCancellationRequested.Should().BeFalse();
    }

    [TestMethod]
    public async Task State_changing_request_outside_api_without_csrf_token_is_rejected()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();

        using var response = await TestRequest.Post("/").SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task State_changing_request_with_valid_csrf_token_is_accepted()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);

        using var response = await TestRequest.Post(ExitPath)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithSession(session)
            .WithCsrfToken(session.CsrfToken)
            .SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }
}
