using System.Net;
using System.Text.Json;
using DTech.Core.Updates;
using DTech.Entrypoint.Tests.Infrastructure;
using DTech.Entrypoint.Updates;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace DTech.Entrypoint.Tests.Updates;

[TestClass]
public sealed class UpdateEndpointsTests
{
    private const string StatusPath = "/api/updates";
    private const string CheckPath = "/api/updates/check";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Status_without_session_is_unauthorized()
    {
        var handler = StubReleaseHandler.Json("[]");
        await using var factory = CreateFactory(handler, false);
        using var client = factory.CreateClientWithoutCookies();

        using var response = await TestRequest.Get(StatusPath).SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        handler.RequestCount.Should().Be(0);
    }

    [TestMethod]
    public async Task Check_without_session_is_unauthorized()
    {
        var handler = StubReleaseHandler.Json("[]");
        await using var factory = CreateFactory(handler, false);
        using var client = factory.CreateClientWithoutCookies();

        using var response = await TestRequest.Post(CheckPath)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        handler.RequestCount.Should().Be(0);
    }

    [TestMethod]
    public async Task Status_reports_available_update_and_caches_it()
    {
        var handler = StubReleaseHandler.Json(StubReleaseHandler.ReleasesJson(("v99.0.0", false, false)));
        await using var factory = CreateFactory(handler, true);
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);

        var first = await GetStatus(client, session);
        var second = await GetStatus(client, session);

        first.GetProperty("state").GetString().Should().Be("updateavailable");
        first.GetProperty("latestVersion").GetString().Should().Be("99.0.0");
        first.GetProperty("releaseUrl").GetString().Should().Be("https://github.com/DanilChizhikov/termforge/releases/tag/v99.0.0");
        first.GetProperty("currentVersion").GetString().Should().NotBeNullOrEmpty();
        second.GetProperty("state").GetString().Should().Be("updateavailable");
        handler.RequestCount.Should().Be(1);
    }

    [TestMethod]
    public async Task Status_reports_up_to_date_when_latest_is_not_newer()
    {
        var handler = StubReleaseHandler.Json(StubReleaseHandler.ReleasesJson(("v0.0.1", false, false)));
        await using var factory = CreateFactory(handler, true);
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);

        var status = await GetStatus(client, session);

        status.GetProperty("state").GetString().Should().Be("uptodate");
    }

    [TestMethod]
    public async Task Status_without_startup_check_does_not_contact_network()
    {
        var handler = StubReleaseHandler.Json(StubReleaseHandler.ReleasesJson(("v99.0.0", false, false)));
        await using var factory = CreateFactory(handler, false);
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);

        var status = await GetStatus(client, session);

        status.GetProperty("state").GetString().Should().Be("notchecked");
        handler.RequestCount.Should().Be(0);
    }

    [TestMethod]
    public async Task Check_runs_on_demand_even_when_startup_check_is_disabled()
    {
        var handler = StubReleaseHandler.Json(StubReleaseHandler.ReleasesJson(("v99.0.0", false, false)));
        await using var factory = CreateFactory(handler, false);
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);

        var checkedStatus = await PostCheck(client, session);
        var cachedStatus = await GetStatus(client, session);

        checkedStatus.GetProperty("state").GetString().Should().Be("updateavailable");
        cachedStatus.GetProperty("state").GetString().Should().Be("updateavailable");
        handler.RequestCount.Should().Be(1);
    }

    [TestMethod]
    public async Task Check_bypasses_cache()
    {
        var handler = StubReleaseHandler.Json(StubReleaseHandler.ReleasesJson(("v99.0.0", false, false)));
        await using var factory = CreateFactory(handler, true);
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);

        await GetStatus(client, session);
        await PostCheck(client, session);

        handler.RequestCount.Should().Be(2);
    }

    [TestMethod]
    public async Task Network_failure_is_reported_quietly_with_success_status()
    {
        var handler = StubReleaseHandler.Throwing();
        await using var factory = CreateFactory(handler, true);
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);

        var status = await PostCheck(client, session);

        status.GetProperty("state").GetString().Should().Be("checkfailed");
        status.GetProperty("latestVersion").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [TestMethod]
    public async Task Startup_check_runs_in_background_when_enabled()
    {
        var handler = StubReleaseHandler.Json(StubReleaseHandler.ReleasesJson(("v99.0.0", false, false)));
        await using var factory = CreateFactory(handler, true, true);
        using var client = factory.CreateClientWithoutCookies();
        await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);

        var checker = factory.Services.GetRequiredService<IUpdateChecker>();
        var status = await WaitForCheck(checker);

        status.State.Should().Be(UpdateState.UpdateAvailable);
        handler.RequestCount.Should().Be(1);
    }

    [TestMethod]
    public async Task Startup_does_not_wait_for_slow_update_check()
    {
        var release = new TaskCompletionSource<HttpResponseMessage>();
        var handler = new StubReleaseHandler(_ => release.Task);
        await using var factory = CreateFactory(handler, true, true);
        using var client = factory.CreateClientWithoutCookies();

        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);
        release.SetResult(new HttpResponseMessage(HttpStatusCode.OK));

        session.SessionToken.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task Startup_check_failure_does_not_break_the_application()
    {
        var handler = StubReleaseHandler.Throwing();
        await using var factory = CreateFactory(handler, true, true);
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);

        var checker = factory.Services.GetRequiredService<IUpdateChecker>();
        var status = await WaitForCheck(checker);

        session.SessionToken.Should().NotBeNullOrEmpty();
        status.State.Should().Be(UpdateState.CheckFailed);
    }

    [TestMethod]
    public async Task Startup_check_is_skipped_when_disabled()
    {
        var handler = StubReleaseHandler.Json("[]");
        await using var factory = CreateFactory(handler, false, true);
        using var client = factory.CreateClientWithoutCookies();
        await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);

        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.CancellationToken);

        handler.RequestCount.Should().Be(0);
    }

    [TestMethod]
    public void Current_version_comes_from_the_assembly()
    {
        var provider = new AssemblyVersionProvider();

        provider.Current.CompareTo(SemanticVersionOf("0.0.0")).Should().BePositive();
    }

    private static SemanticVersion SemanticVersionOf(string text)
    {
        SemanticVersion.TryParse(text, out var version);
        return version!;
    }

    private static TermForgeApplicationFactory CreateFactory(StubReleaseHandler handler, bool checkOnStartup, bool startBackgroundWork = false)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Updates:CheckOnStartup"] = checkOnStartup ? "true" : "false",
        };
        var factory = new TermForgeApplicationFactory(settings)
        {
            ConfigureServices = services => services
                .AddHttpClient<IReleaseSource, GitHubReleaseSource>()
                .ConfigurePrimaryHttpMessageHandler(() => handler),
        };
        if (startBackgroundWork)
        {
            _ = factory.Server;
        }

        return factory;
    }

    private async Task<UpdateStatus> WaitForCheck(IUpdateChecker checker)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var status = checker.Peek();
            if (status.State != UpdateState.NotChecked)
            {
                return status;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), TestContext.CancellationToken);
        }

        return checker.Peek();
    }

    private async Task<JsonElement> GetStatus(HttpClient client, AuthenticatedSession session)
    {
        using var response = await TestRequest.Get(StatusPath).WithSession(session).SendAsync(client, TestContext.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadJson(response);
    }

    private async Task<JsonElement> PostCheck(HttpClient client, AuthenticatedSession session)
    {
        using var response = await TestRequest.Post(CheckPath)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithSession(session)
            .WithCsrfToken(session.CsrfToken)
            .SendAsync(client, TestContext.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadJson(response);
    }

    private async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }
}
