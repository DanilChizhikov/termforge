using System.Net;
using DTech.Core.Updates;
using DTech.Entrypoint.Updates;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.Tests.Updates;

[TestClass]
public sealed class GitHubReleaseSourceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Picks_highest_published_release()
    {
        var body = StubReleaseHandler.ReleasesJson(("v0.2.0", false, false), ("v0.10.0", false, false), ("v0.9.0", false, false));
        var source = CreateSource(StubReleaseHandler.Json(body));

        var lookup = await source.GetLatestRelease(TestContext.CancellationToken);

        lookup.Succeeded.Should().BeTrue();
        lookup.Release!.Version.ToString().Should().Be("0.10.0");
        lookup.Release.Url.Should().Be("https://github.com/DanilChizhikov/termforge/releases/tag/v0.10.0");
    }

    [TestMethod]
    public async Task Ignores_drafts_and_prereleases()
    {
        var body = StubReleaseHandler.ReleasesJson(("v9.0.0", true, false), ("v8.0.0", false, true), ("v7.0.0-rc.1", false, false), ("v0.3.0", false, false));
        var source = CreateSource(StubReleaseHandler.Json(body));

        var lookup = await source.GetLatestRelease(TestContext.CancellationToken);

        lookup.Release!.Version.ToString().Should().Be("0.3.0");
    }

    [TestMethod]
    public async Task Ignores_releases_with_unparseable_tags()
    {
        var body = StubReleaseHandler.ReleasesJson(("nightly", false, false), ("v0.3.0", false, false));
        var source = CreateSource(StubReleaseHandler.Json(body));

        var lookup = await source.GetLatestRelease(TestContext.CancellationToken);

        lookup.Release!.Version.ToString().Should().Be("0.3.0");
    }

    [TestMethod]
    public async Task Ignores_releases_with_untrusted_urls()
    {
        const string body = "[{\"tag_name\":\"v5.0.0\",\"html_url\":\"https://evil.example/releases/v5.0.0\",\"draft\":false,\"prerelease\":false},"
            + "{\"tag_name\":\"v4.0.0\",\"html_url\":\"javascript:alert(1)\",\"draft\":false,\"prerelease\":false}]";
        var source = CreateSource(StubReleaseHandler.Json(body));

        var lookup = await source.GetLatestRelease(TestContext.CancellationToken);

        lookup.Succeeded.Should().BeTrue();
        lookup.Release.Should().BeNull();
    }

    [TestMethod]
    public async Task Reports_no_release_for_empty_list()
    {
        var source = CreateSource(StubReleaseHandler.Json("[]"));

        var lookup = await source.GetLatestRelease(TestContext.CancellationToken);

        lookup.Succeeded.Should().BeTrue();
        lookup.Release.Should().BeNull();
    }

    [TestMethod]
    [DataRow(HttpStatusCode.NotFound)]
    [DataRow(HttpStatusCode.Forbidden)]
    [DataRow(HttpStatusCode.InternalServerError)]
    public async Task Fails_quietly_on_error_status(HttpStatusCode statusCode)
    {
        var source = CreateSource(StubReleaseHandler.Status(statusCode));

        var lookup = await source.GetLatestRelease(TestContext.CancellationToken);

        lookup.Succeeded.Should().BeFalse();
    }

    [TestMethod]
    public async Task Fails_quietly_on_network_error()
    {
        var source = CreateSource(StubReleaseHandler.Throwing());

        var lookup = await source.GetLatestRelease(TestContext.CancellationToken);

        lookup.Succeeded.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("not json")]
    [DataRow("{\"unexpected\":true}")]
    public async Task Fails_quietly_on_malformed_payload(string body)
    {
        var source = CreateSource(StubReleaseHandler.Json(body));

        var lookup = await source.GetLatestRelease(TestContext.CancellationToken);

        lookup.Succeeded.Should().BeFalse();
    }

    [TestMethod]
    public async Task Fails_quietly_on_timeout()
    {
        var handler = new StubReleaseHandler(_ => throw new TaskCanceledException("timeout"));
        var source = CreateSource(handler);

        var lookup = await source.GetLatestRelease(TestContext.CancellationToken);

        lookup.Succeeded.Should().BeFalse();
    }

    [TestMethod]
    public async Task Propagates_caller_cancellation()
    {
        var handler = new StubReleaseHandler(_ => throw new TaskCanceledException("cancelled"));
        var source = CreateSource(handler);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var act = () => source.GetLatestRelease(cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [TestMethod]
    public async Task Requests_releases_of_configured_repository()
    {
        var handler = StubReleaseHandler.Json("[]");
        var source = CreateSource(handler);

        await source.GetLatestRelease(TestContext.CancellationToken);

        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/repos/DanilChizhikov/termforge/releases");
    }

    private static GitHubReleaseSource CreateSource(StubReleaseHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var updateOptions = new UpdateOptions();
        var options = Options.Create(updateOptions);
        var logger = NullLogger<GitHubReleaseSource>.Instance;
        return new GitHubReleaseSource(client, options, logger);
    }
}
