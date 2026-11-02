using DTech.Core.Credentials;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.SettingsTransfer;

[TestClass]
public sealed class CredentialMutationCoordinatorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ImportLease_BlocksNormalWritesDeletesAndReadsUntilReleased()
    {
        var raw = Substitute.For<IUncoordinatedCredentialStore>();
        using var coordinator = new CredentialMutationCoordinator();
        var store = new CoordinatedCredentialStore(raw, coordinator);
        var key = CredentialKey.Create("test-key").Value;
        using var secret = Secret.FromString("replacement");
        var success = CredentialResult.Success();
        var readError = CredentialErrors.NotFound(key);
        var missing = CredentialResult<Secret>.Failure(readError);
        raw.Store(key, secret, Arg.Any<CancellationToken>()).Returns(success);
        raw.Delete(key, Arg.Any<CancellationToken>()).Returns(success);
        raw.Read(key, Arg.Any<CancellationToken>()).Returns(missing);
        var lease = await coordinator.AcquireAsync(TestContext.CancellationToken);

        var writing = store.Store(key, secret, TestContext.CancellationToken);
        var deleting = store.Delete(key, TestContext.CancellationToken);
        var reading = store.Read(key, TestContext.CancellationToken);

        writing.IsCompleted.Should().BeFalse();
        deleting.IsCompleted.Should().BeFalse();
        reading.IsCompleted.Should().BeFalse();
        await raw.DidNotReceiveWithAnyArgs().Store(default, default!, default);
        await raw.DidNotReceiveWithAnyArgs().Delete(default, default);
        await raw.DidNotReceiveWithAnyArgs().Read(default, default);
        lease.Dispose();
        await Task.WhenAll(writing, deleting, reading);
        await raw.Received(1).Store(key, secret, Arg.Any<CancellationToken>());
        await raw.Received(1).Delete(key, Arg.Any<CancellationToken>());
        await raw.Received(1).Read(key, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task WaitingWriteCancellation_DoesNotMutateOrPreventTheNextLease()
    {
        var raw = Substitute.For<IUncoordinatedCredentialStore>();
        using var coordinator = new CredentialMutationCoordinator();
        var store = new CoordinatedCredentialStore(raw, coordinator);
        var key = CredentialKey.Create("test-key").Value;
        using var secret = Secret.FromString("replacement");
        using var cancellation = new CancellationTokenSource();
        var lease = await coordinator.AcquireAsync(TestContext.CancellationToken);
        var writing = store.Store(key, secret, cancellation.Token);

        await cancellation.CancelAsync();
        var awaiting = () => writing;
        await awaiting.Should().ThrowAsync<OperationCanceledException>();
        await raw.DidNotReceiveWithAnyArgs().Store(default, default!, default);
        lease.Dispose();
        using var next = await coordinator.AcquireAsync(TestContext.CancellationToken);
        next.Should().NotBeNull();
    }
}
