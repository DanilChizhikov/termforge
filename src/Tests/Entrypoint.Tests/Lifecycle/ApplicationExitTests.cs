using System.Net;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.Lifecycle;

[TestClass]
public sealed class ApplicationExitTests
{
    private const string ExitPath = "/api/lifecycle/exit";
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(10);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Exit_endpoint_stops_the_host()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);
        var stopping = WaitForCancellation(factory.ApplicationStopping);

        using var response = await TestRequest.Post(ExitPath)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithSession(session)
            .WithCsrfToken(session.CsrfToken)
            .SendAsync(client, TestContext.CancellationToken);
        var completed = await Task.WhenAny(stopping, Task.Delay(StopTimeout, TestContext.CancellationToken));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        completed.Should().BeSameAs(stopping);
    }

    private static Task WaitForCancellation(CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        cancellationToken.Register(completion.SetResult);
        return completion.Task;
    }
}
