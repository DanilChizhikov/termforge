using DTech.Core.Credentials;
using DTech.Credentials.Native;
using DTech.Credentials.Native.Processes;
using DTech.Credentials.Tests.Support;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace DTech.Credentials.Tests.Native;

[TestClass]
public sealed class CredentialHelperRunnerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Deadline_KillsHangingHelperAndDoesNotReportSuccessOrNotFound()
    {
        var factory = new SleepingCredentialHelperFactory();
        var runner = CreateRunner(factory, TimeSpan.FromMilliseconds(200));
        var metadata = new CredentialHelperRequest(CredentialHelperOperation.Store, "private-key", 200);
        byte[] bytes = [65, 66, 67];
        using var request = new CredentialHelperPacket<CredentialHelperRequest>(metadata, bytes);

        using var response = await runner.RunAsync(request, TestContext.CancellationToken);

        response.Metadata.Error!.Code.Should().Be(CredentialErrorCode.StoreUnavailable);
        response.Metadata.Error.Message.Should().Contain("result is unknown");
        factory.StartedIds.Should().ContainSingle();
        factory.AnyRunning().Should().BeFalse();
    }

    [TestMethod]
    public async Task CallerCancellation_KillsHelperBeforeReturning()
    {
        var factory = new SleepingCredentialHelperFactory();
        var runner = CreateRunner(factory, TimeSpan.FromSeconds(10));
        var metadata = new CredentialHelperRequest(CredentialHelperOperation.Read, "private-key", 10000);
        using var request = new CredentialHelperPacket<CredentialHelperRequest>(metadata, []);
        using var cancellation = new CancellationTokenSource();
        var running = runner.RunAsync(request, cancellation.Token);
        await factory.Started.Task.WaitAsync(TestContext.CancellationToken);

        await cancellation.CancelAsync();
        var awaiting = () => running;

        await awaiting.Should().ThrowAsync<OperationCanceledException>();
        factory.AnyRunning().Should().BeFalse();
    }

    [TestMethod]
    public async Task ProbeDeadline_WaitsForProductionHelperCleanup()
    {
        var factory = new SleepingCredentialHelperFactory();
        var settings = new CredentialStoreOptions
        {
            ProbeTimeout = TimeSpan.FromMilliseconds(200),
            OperationTimeout = TimeSpan.FromSeconds(10),
        };
        var options = Options.Create(settings);
        var runner = new CredentialHelperRunner(factory, options);
        var raw = new InMemoryOperatingSystemStore();
        var store = new ProcessCredentialStore(raw, runner);
        var probe = new OperatingSystemCredentialStoreProbe(store, options);

        var availability = await probe.Probe(TestContext.CancellationToken);

        availability.IsAvailable.Should().BeFalse();
        availability.Diagnostic.Should().Contain("did not respond");
        factory.StartedIds.Should().ContainSingle();
        factory.AnyRunning().Should().BeFalse();
    }

    [TestMethod]
    public async Task ProbeCancellation_WaitsForProductionHelperCleanup()
    {
        var factory = new SleepingCredentialHelperFactory();
        var settings = new CredentialStoreOptions();
        var options = Options.Create(settings);
        var runner = new CredentialHelperRunner(factory, options);
        var raw = new InMemoryOperatingSystemStore();
        var store = new ProcessCredentialStore(raw, runner);
        var probe = new OperatingSystemCredentialStoreProbe(store, options);
        using var cancellation = new CancellationTokenSource();
        var probing = probe.Probe(cancellation.Token);
        await factory.Started.Task.WaitAsync(TestContext.CancellationToken);

        await cancellation.CancelAsync();
        var awaiting = () => probing;

        await awaiting.Should().ThrowAsync<OperationCanceledException>();
        factory.AnyRunning().Should().BeFalse();
    }

    [TestMethod]
    public async Task ProcessStore_CopiesSecretBeforeAwaitingHelper()
    {
        var factory = new SleepingCredentialHelperFactory();
        var runner = CreateRunner(factory, TimeSpan.FromMilliseconds(200));
        var raw = new InMemoryOperatingSystemStore();
        var store = new ProcessCredentialStore(raw, runner);
        var key = TestKeys.Create("private-key");
        var secret = Secret.FromString("private-value");

        var storing = store.Store(key, secret, TestContext.CancellationToken);
        secret.Dispose();
        var result = await storing;

        result.Error!.Code.Should().Be(CredentialErrorCode.StoreUnavailable);
        factory.AnyRunning().Should().BeFalse();
    }

    [TestMethod]
    public async Task ConcurrentCalls_StartAtMostTwoHelpers()
    {
        var factory = new SleepingCredentialHelperFactory();
        var runner = CreateRunner(factory, TimeSpan.FromSeconds(10));
        var metadata = new CredentialHelperRequest(CredentialHelperOperation.Read, "private-key", 10000);
        using var firstRequest = new CredentialHelperPacket<CredentialHelperRequest>(metadata, []);
        using var secondRequest = new CredentialHelperPacket<CredentialHelperRequest>(metadata, []);
        using var thirdRequest = new CredentialHelperPacket<CredentialHelperRequest>(metadata, []);
        using var all = new CancellationTokenSource();
        using var thirdCancellation = new CancellationTokenSource();
        var first = runner.RunAsync(firstRequest, all.Token);
        var second = runner.RunAsync(secondRequest, all.Token);
        var third = runner.RunAsync(thirdRequest, thirdCancellation.Token);

        var startedCount = 0;
        try
        {
            await thirdCancellation.CancelAsync();
            var awaitingThird = () => third;
            await awaitingThird.Should().ThrowAsync<OperationCanceledException>();
            startedCount = factory.StartedIds.Count;
        }
        finally
        {
            await all.CancelAsync();
        }

        var awaitingFirst = () => first;
        var awaitingSecond = () => second;
        await awaitingFirst.Should().ThrowAsync<OperationCanceledException>();
        await awaitingSecond.Should().ThrowAsync<OperationCanceledException>();
        startedCount.Should().Be(2);
        factory.AnyRunning().Should().BeFalse();
    }

    private static CredentialHelperRunner CreateRunner(ICredentialHelperProcessFactory factory, TimeSpan timeout)
    {
        var settings = new CredentialStoreOptions { OperationTimeout = timeout };
        var options = Options.Create(settings);
        return new CredentialHelperRunner(factory, options);
    }
}
