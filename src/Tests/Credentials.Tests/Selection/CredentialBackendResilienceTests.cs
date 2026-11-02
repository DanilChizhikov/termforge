using DTech.Core.Credentials;
using DTech.Credentials.Native;
using DTech.Credentials.Selection;
using DTech.Credentials.Tests.Support;
using DTech.Credentials.Vault;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DTech.Credentials.Tests.Selection;

[TestClass]
public sealed class CredentialBackendResilienceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task FailedProbe_IsRetriedAfterBackoffWithoutCreatingVault()
    {
        using var context = new VaultTestContext();
        var time = new ManualTimeProvider();
        var raw = CreateRecoveringStore();
        var selector = CreateSelector(raw, context, time);

        var first = await selector.Select(TestContext.CancellationToken);
        var cachedFailure = await selector.Select(TestContext.CancellationToken);
        time.Advance(CredentialBackendSelector.FailedProbeRetryDelay);
        var recovered = await selector.Select(TestContext.CancellationToken);

        first.Backend.Should().Be(CredentialBackend.EncryptedVault);
        first.Reason.Should().Contain("have not been migrated");
        cachedFailure.Should().BeSameAs(first);
        recovered.Backend.Should().Be(CredentialBackend.OperatingSystemStore);
        recovered.Store.Should().BeSameAs(raw);
        File.Exists(context.VaultPath).Should().BeFalse();
        raw.Received(2).CheckPrerequisites();
    }

    [TestMethod]
    public async Task ConcurrentRequests_ReuseOneSuccessfulRecoveryProbe()
    {
        using var context = new VaultTestContext();
        var time = new ManualTimeProvider();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var raw = CreateRecoveringStore(release.Task);
        var selector = CreateSelector(raw, context, time);
        await selector.Select(TestContext.CancellationToken);
        time.Advance(CredentialBackendSelector.FailedProbeRetryDelay);
        var selections = new Task<CredentialBackendSelection>[16];

        for (var i = 0; i < selections.Length; i++)
        {
            selections[i] = selector.Select(TestContext.CancellationToken);
        }

        release.SetResult();
        var recovered = await Task.WhenAll(selections);

        recovered.Should().OnlyContain(selection => selection.Backend == CredentialBackend.OperatingSystemStore);
        raw.Received(2).CheckPrerequisites();
        await raw.Received(1).Store(Arg.Any<CredentialKey>(), Arg.Any<Secret>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task VaultCreatedAfterFailure_StaysSelectedAfterOperatingSystemRecovery()
    {
        using var context = new VaultTestContext();
        var time = new ManualTimeProvider();
        var raw = CreateRecoveringStore();
        var selector = CreateSelector(raw, context, time);
        var fallback = await selector.Select(TestContext.CancellationToken);
        var vault = (EncryptedVault)fallback.Store;
        using var password = Secret.FromString(VaultTestContext.MasterPassword);
        await vault.Unlock(password, TestContext.CancellationToken);
        time.Advance(CredentialBackendSelector.FailedProbeRetryDelay);

        var selected = await selector.Select(TestContext.CancellationToken);

        selected.Backend.Should().Be(CredentialBackend.EncryptedVault);
        selected.Reason.Should().Contain("have not been migrated");
        raw.Received(1).CheckPrerequisites();
    }

    [TestMethod]
    public async Task MissingVaultEntry_WithUnavailableOperatingSystem_DoesNotMaskFailureAsNotFound()
    {
        using var context = new VaultTestContext();
        var time = new ManualTimeProvider();
        var raw = CreateRecoveringStore();
        var selector = CreateSelector(raw, context, time);
        var fallback = await selector.Select(TestContext.CancellationToken);
        var vault = (EncryptedVault)fallback.Store;
        using var password = Secret.FromString(VaultTestContext.MasterPassword);
        await vault.Unlock(password, TestContext.CancellationToken);
        var store = new CredentialStore(selector);
        var key = TestKeys.Create("missing");

        var result = await store.Read(key, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(CredentialErrorCode.StoreUnavailable);
        result.Error.Message.Should().Contain("have not been migrated");
    }

    private static CredentialBackendSelector CreateSelector(IOperatingSystemCredentialStore raw, VaultTestContext context, TimeProvider time)
    {
        var vault = context.CreateVault();
        var probe = new OperatingSystemCredentialStoreProbe(raw, context.Options);
        var logger = NullLogger<CredentialBackendSelector>.Instance;
        return new CredentialBackendSelector(raw, probe, vault, logger, time);
    }

    private static IOperatingSystemCredentialStore CreateRecoveringStore(Task? beforeStore = null)
    {
        var memory = new InMemoryOperatingSystemStore();
        var raw = Substitute.For<IOperatingSystemCredentialStore>();
        raw.Name.Returns("recovering store");
        var unavailable = CredentialStoreAvailability.Unavailable(raw.Name, "temporarily locked");
        var available = CredentialStoreAvailability.Available(raw.Name);
        raw.CheckPrerequisites().Returns(unavailable, available);
        raw.Store(Arg.Any<CredentialKey>(), Arg.Any<Secret>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var key = call.Arg<CredentialKey>();
                var secret = call.Arg<Secret>();
                var cancellationToken = call.Arg<CancellationToken>();
                if (beforeStore is not null)
                {
                    await beforeStore.WaitAsync(cancellationToken);
                }

                return await memory.Store(key, secret, cancellationToken);
            });
        raw.Read(Arg.Any<CredentialKey>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var key = call.Arg<CredentialKey>();
                var cancellationToken = call.Arg<CancellationToken>();
                return memory.Read(key, cancellationToken);
            });
        raw.Delete(Arg.Any<CredentialKey>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var key = call.Arg<CredentialKey>();
                var cancellationToken = call.Arg<CancellationToken>();
                return memory.Delete(key, cancellationToken);
            });
        return raw;
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }

        public void Advance(TimeSpan elapsed)
        {
            _now += elapsed;
        }
    }
}
