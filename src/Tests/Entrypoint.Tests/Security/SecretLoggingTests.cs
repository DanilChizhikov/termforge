using System.Net;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.Security;

[TestClass]
public sealed class SecretLoggingTests
{
    private static readonly Dictionary<string, string?> TraceLoggingSettings = new()
    {
        ["Logging:LogLevel:Default"] = "Trace",
        ["Logging:LogLevel:Microsoft"] = "Trace",
        ["Logging:LogLevel:Microsoft.AspNetCore"] = "Trace",
    };

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Secrets_are_never_logged_at_any_level()
    {
        await using var factory = new TermForgeApplicationFactory(TraceLoggingSettings);
        using var client = factory.CreateClientWithoutCookies();
        var bootstrapToken = factory.BootstrapTokenValue;

        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);
        using var replayResponse = await SessionBootstrapper.ExchangeAsync(client, bootstrapToken, TestContext.CancellationToken);
        using var rejectedResponse = await TestRequest.Post("/api/lifecycle/exit")
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithSession(session)
            .WithCsrfToken("wrong-token")
            .SendAsync(client, TestContext.CancellationToken);
        using var exitResponse = await TestRequest.Post("/api/lifecycle/exit")
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithSession(session)
            .WithCsrfToken(session.CsrfToken)
            .SendAsync(client, TestContext.CancellationToken);

        replayResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        rejectedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        exitResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var messages = factory.Logs.Messages;
        messages.Should().Contain(message => message.Contains("Microsoft.AspNetCore.Hosting.Diagnostics"));
        messages.Should().NotContain(message => message.Contains(bootstrapToken));
        messages.Should().NotContain(message => message.Contains(session.SessionToken));
        messages.Should().NotContain(message => message.Contains(session.CsrfToken));
    }
}
