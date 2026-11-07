using System.Net;
using System.Text.Json;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.Updates;

[TestClass]
public sealed class UpdateInstallationEndpointsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Development_launch_reports_why_installation_is_unavailable()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);
        using var response = await TestRequest.Get("/api/updates/installation").WithSession(session).SendAsync(client, TestContext.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var json = JsonDocument.Parse(text);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.GetProperty("supported").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("unavailableReason").GetString().Should().Contain("published");
    }

    [TestMethod]
    [DataRow("prepare")]
    [DataRow("rollback")]
    [DataRow("cancel")]
    [DataRow("install")]
    public async Task Installation_mutations_require_a_session(string operation)
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();
        using var response = await TestRequest.Post($"/api/updates/{operation}")
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin).SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Preparation_rejects_development_installations_without_stopping_the_host()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);
        using var response = await TestRequest.Post("/api/updates/prepare")
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin).WithSession(session).WithCsrfToken(session.CsrfToken)
            .SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        factory.ApplicationStopping.IsCancellationRequested.Should().BeFalse();
    }

    [TestMethod]
    public async Task Preparation_requires_csrf_proof_even_with_a_session()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);
        using var response = await TestRequest.Post("/api/updates/prepare")
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin).WithSession(session)
            .SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
