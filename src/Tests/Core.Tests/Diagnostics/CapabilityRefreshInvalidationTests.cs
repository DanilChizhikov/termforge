using DTech.Core.Connections;
using DTech.Core.Diagnostics;
using DTech.Core.SavedConnections;
using DTech.Core.Tests.Diagnostics.Support;
using DTech.Core.Tests.SavedConnections.Support;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Diagnostics;

[TestClass]
public sealed class CapabilityRefreshInvalidationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ConnectionMutationWithUnknownCommitOutcome_InvalidatesItsReport(bool delete)
    {
        var id = Guid.NewGuid();
        var draft = new SavedConnectionDraft { Name = "web", Host = "example.test", UserName = "deploy" };
        var connection = new SavedConnection { Id = id, Details = draft };
        IReadOnlyList<SavedConnection> connections = [connection];
        var repository = Substitute.For<ISavedConnectionRepository>();
        repository.FindAsync(id, Arg.Any<CancellationToken>()).Returns(connection);
        repository.ListAsync(default).ReturnsForAnyArgs(connections);
        var failure = new IOException("The database write outcome is unknown.");
        var failedWrite = Task.FromException<bool>(failure);
        repository.UpdateAsync(default!, default).ReturnsForAnyArgs(failedWrite);
        repository.DeleteAsync(default, default).ReturnsForAnyArgs(failedWrite);
        var credentials = new InMemoryCredentialStore();
        var passwords = new ConnectionPasswordStore(credentials);
        var cleaner = new ConnectionSecretCleaner(credentials);
        var cache = new CapabilityReportCache();
        var report = new CapabilityReport(id, DateTime.UtcNow, CapabilityReportState.Completed, []);
        cache.Store(report);
        var service = new SavedConnectionService(repository, passwords, cleaner, TimeProvider.System, cache);
        var submission = new SavedConnectionSubmission(draft, null);

        var mutate = async () =>
        {
            if (delete)
            {
                await service.DeleteAsync(id, TestContext.CancellationToken);
            }
            else
            {
                await service.UpdateAsync(id, submission, TestContext.CancellationToken);
            }
        };
        await mutate.Should().ThrowAsync<IOException>();

        cache.Find(id).Should().BeNull();
    }

    [TestMethod]
    public async Task RefreshCompletingAfterInvalidation_DoesNotRestoreStaleCapabilities()
    {
        var repository = new InMemorySavedConnectionRepository();
        var id = Guid.NewGuid();
        var draft = new SavedConnectionDraft { Name = "web", Host = "old.example.test", UserName = "deploy" };
        var connection = new SavedConnection { Id = id, Details = draft };
        await repository.AddAsync(connection, TestContext.CancellationToken);
        var credentials = new InMemoryCredentialStore();
        var passwords = new ConnectionPasswordStore(credentials);
        var targets = new ConnectionTargetFactory(passwords);
        var runners = Substitute.For<IRemoteCommandRunnerFactory>();
        var runner = new ScriptedCommandRunner();
        var connected = SshResult<IRemoteCommandRunner>.Success(runner);
        runners.ConnectAsync(default!, default).ReturnsForAnyArgs(connected);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource<IReadOnlyList<CapabilityResult>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = Substitute.For<ICapabilityProbe>();
        probe.ProbeAsync(default!, default).ReturnsForAnyArgs(_ =>
        {
            started.TrySetResult();
            return finish.Task;
        });
        var cache = new CapabilityReportCache();
        var probes = new[] { probe };
        var service = new CapabilityDiagnosticsService(repository, targets, runners, probes, cache, TimeProvider.System);

        var refreshing = service.RefreshAsync(id, TestContext.CancellationToken);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        var changed = connection with { Details = draft with { Host = "new.example.test" } };
        await repository.UpdateAsync(changed, TestContext.CancellationToken);
        var connections = new[] { changed };
        cache.Invalidate(id, connections);
        var oldCapability = CapabilityResult.Available("old-host", "Old host", "old");
        finish.TrySetResult([oldCapability]);
        var result = await refreshing;

        result.IsSuccess.Should().BeTrue();
        cache.Find(id).Should().BeNull();
        await service.GetAsync(id, TestContext.CancellationToken);
        await runners.ReceivedWithAnyArgs(2).ConnectAsync(default!, default);
    }
}
