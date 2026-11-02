using System.Net;
using DTech.Core.Diagnostics;
using DTech.Core.Connections;
using DTech.Entrypoint.Tests.SettingsTransfer.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace DTech.Entrypoint.Tests.Diagnostics;

[TestClass]
public sealed class DiagnosticsCacheInvalidationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task UpdatingJumpConnection_InvalidatesItsDescendantsThroughProductionServices()
    {
        await using var harness = await SettingsTransferApiHarness.StartAsync(TestContext);
        var jump = await harness.CreateConnectionAsync("jump");
        var childOptions = new { jumpHostId = jump };
        var child = await harness.CreateConnectionAsync("child", childOptions);
        var nestedOptions = new { jumpHostId = child };
        var nested = await harness.CreateConnectionAsync("nested", nestedOptions);
        var unrelated = await harness.CreateConnectionAsync("unrelated");
        var cache = harness.Factory.Services.GetRequiredService<CapabilityReportCache>();
        var ids = new[] { jump, child, nested, unrelated };
        for (var i = 0; i < ids.Length; i++)
        {
            var report = Report(ids[i]);
            cache.Store(report);
        }
        var update = new { name = "jump", host = "changed.example.test", port = 22, userName = "deploy", authMethod = "Agent" };
        var path = $"/api/connections/{jump}";

        using var response = await harness.SendAsync(HttpMethod.Put, path, update);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        cache.Find(jump).Should().BeNull();
        cache.Find(child).Should().BeNull();
        cache.Find(nested).Should().BeNull();
        cache.Find(unrelated).Should().NotBeNull();
    }

    [TestMethod]
    public async Task DeletingConnection_InvalidatesItsCachedReport()
    {
        await using var harness = await SettingsTransferApiHarness.StartAsync(TestContext);
        var id = await harness.CreateConnectionAsync("web");
        var cache = harness.Factory.Services.GetRequiredService<CapabilityReportCache>();
        var report = Report(id);
        cache.Store(report);
        var path = $"/api/connections/{id}";

        using var response = await harness.SendAsync(HttpMethod.Delete, path, null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        cache.Find(id).Should().BeNull();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SettingsImport_InvalidatesOnlyAfterSuccessfulCommit(bool failSecretWrite)
    {
        await using var source = await SettingsTransferApiHarness.StartAsync(TestContext);
        await using var target = await SettingsTransferApiHarness.StartAsync(TestContext);
        var importedPassword = new { authMethod = "Password", password = "imported-password" };
        var sourceId = await source.CreateConnectionAsync("web", importedPassword);
        var originalPassword = new { authMethod = "Password", password = "original-password" };
        var targetId = await target.CreateConnectionAsync("web", originalPassword);
        var cache = target.Factory.Services.GetRequiredService<CapabilityReportCache>();
        var report = Report(targetId);
        cache.Store(report);
        var sections = new { connections = true, preferences = false, workspace = false, pinnedCommands = false };
        var exportRequest = new { sections };
        using var exported = await source.SendAsync(HttpMethod.Post, "/api/settings-transfer/export", exportRequest);
        exported.StatusCode.Should().Be(HttpStatusCode.OK);
        var export = await source.ReadJsonAsync(exported);
        var resolutions = new Dictionary<Guid, string> { [sourceId] = "useImported" };
        var import = new
        {
            file = export.GetProperty("file").GetProperty("contentBase64").GetString(),
            key = export.GetProperty("key").GetString(),
            mode = "merge",
            sections,
            resolutions,
        };
        if (failSecretWrite)
        {
            target.CredentialStore.FailStoreCall = target.CredentialStore.StoreCalls + 1;
            target.CredentialStore.MutateBeforeFailure = true;
        }

        using var response = await target.SendAsync(HttpMethod.Post, "/api/settings-transfer/import", import);

        if (failSecretWrite)
        {
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            cache.Find(targetId).Should().BeSameAs(report);
        }
        else
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            cache.Find(targetId).Should().BeNull();
        }
    }

    [TestMethod]
    public async Task Metrics_OverflowedMemory_ReturnsUnavailableWithoutLosingOtherMetrics()
    {
        const string output = "@@os\nLinux\n@@cpu1\ncpu 100 0 0 100\n@@cpu2\ncpu 150 0 0 150\n@@uptime\n3600\n@@mem\nMemTotal: 9223372036854775807 kB\nMemAvailable: 0 kB\n@@disk\n/dev/test 1000 400 600 40% /\n@@loadavg\n0.1 0.2 0.3\n@@cpucount\n2\n";
        var runner = Substitute.For<IRemoteCommandRunner>();
        var execution = new RemoteCommandResult(0, output, string.Empty);
        var executed = SshResult<RemoteCommandResult>.Success(execution);
        runner.RunAsync(default!, default).ReturnsForAnyArgs(executed);
        var factory = Substitute.For<IRemoteCommandRunnerFactory>();
        var connected = SshResult<IRemoteCommandRunner>.Success(runner);
        factory.ConnectAsync(default!, default).ReturnsForAnyArgs(connected);
        await using var harness = await SettingsTransferApiHarness.StartAsync(TestContext, services =>
        {
            services.RemoveAll<IRemoteCommandRunnerFactory>();
            services.AddSingleton(factory);
        });
        var id = await harness.CreateConnectionAsync("web");
        var path = $"/api/connections/{id}/metrics";

        using var response = await harness.SendAsync(HttpMethod.Get, path, null);
        var body = await harness.ReadJsonAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("memory").GetProperty("available").GetBoolean().Should().BeFalse();
        body.GetProperty("cpuPercent").GetProperty("available").GetBoolean().Should().BeTrue();
        body.GetProperty("disk").GetProperty("available").GetBoolean().Should().BeTrue();
        body.GetProperty("uptimeSeconds").GetProperty("value").GetDouble().Should().Be(3600);
    }

    private static CapabilityReport Report(Guid id)
    {
        return new CapabilityReport(id, DateTime.UtcNow, CapabilityReportState.Completed, []);
    }
}
