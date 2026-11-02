using DTech.Core.Credentials;
using DTech.Core.SettingsTransfer;
using DTech.Core.SettingsTransfer.Import;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.SettingsTransfer;

[TestClass]
public sealed class ImportSecretTransactionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Rollback_RestoresAttemptedKeysInReverseOrderIncludingFailedMutation()
    {
        var store = Substitute.For<IUncoordinatedCredentialStore>();
        var first = CredentialKey.Create("first").Value;
        var second = CredentialKey.Create("second").Value;
        var original = Secret.FromString("original");
        var found = CredentialResult<Secret>.Success(original);
        var missingError = CredentialErrors.NotFound(second);
        var missing = CredentialResult<Secret>.Failure(missingError);
        store.Read(first, Arg.Any<CancellationToken>()).Returns(found);
        store.Read(second, Arg.Any<CancellationToken>()).Returns(missing);
        var success = CredentialResult.Success();
        var failureError = CredentialErrors.StoreUnavailable("unknown native result");
        var failure = CredentialResult.Failure(failureError);
        var mutations = new List<string>();
        store.Store(first, Arg.Any<Secret>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            mutations.Add($"first:{call.Arg<Secret>().RevealString()}");
            return success;
        });
        store.Store(second, Arg.Any<Secret>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            mutations.Add("second:failed");
            return failure;
        });
        store.Delete(second, Arg.Any<CancellationToken>()).Returns(call =>
        {
            mutations.Add("second:deleted");
            return success;
        });
        using var transaction = new ImportSecretTransaction(store);
        CredentialKey[] keys = [first, second];
        await transaction.CaptureAsync(keys, TestContext.CancellationToken);
        using var replacement = Secret.FromString("replacement");
        await transaction.Store(first, replacement, TestContext.CancellationToken);

        var failing = () => transaction.Store(second, replacement, TestContext.CancellationToken);
        await failing.Should().ThrowAsync<SettingsImportException>();
        var restored = await transaction.RollbackAsync(TestContext.CancellationToken);

        restored.Should().BeTrue();
        mutations.Should().Equal("first:replacement", "second:failed", "second:deleted", "first:original");
        transaction.Dispose();
        var accessing = () => original.Length;
        accessing.Should().Throw<ObjectDisposedException>();
    }

    [TestMethod]
    public async Task Capture_DoesNotTreatUnavailableStoreAsMissingAndDisposesEarlierSnapshots()
    {
        var store = Substitute.For<IUncoordinatedCredentialStore>();
        var first = CredentialKey.Create("first").Value;
        var second = CredentialKey.Create("second").Value;
        var original = Secret.FromString("original");
        var found = CredentialResult<Secret>.Success(original);
        var unavailableError = CredentialErrors.StoreUnavailable("locked");
        var unavailable = CredentialResult<Secret>.Failure(unavailableError);
        store.Read(first, Arg.Any<CancellationToken>()).Returns(found);
        store.Read(second, Arg.Any<CancellationToken>()).Returns(unavailable);
        using var transaction = new ImportSecretTransaction(store);
        CredentialKey[] keys = [first, second];

        var capturing = () => transaction.CaptureAsync(keys, TestContext.CancellationToken);

        var failure = await capturing.Should().ThrowAsync<SettingsImportException>();
        failure.Which.Error.Code.Should().Be(SettingsTransferErrorCode.SecretsUnavailable);
        await store.DidNotReceiveWithAnyArgs().Store(default, default!, default);
        await store.DidNotReceiveWithAnyArgs().Delete(default, default);
        transaction.Dispose();
        var accessing = () => original.Length;
        accessing.Should().Throw<ObjectDisposedException>();
    }

    [TestMethod]
    public async Task Rollback_ReportsFailureAndStillRestoresRemainingKeys()
    {
        var store = Substitute.For<IUncoordinatedCredentialStore>();
        var first = CredentialKey.Create("first").Value;
        var second = CredentialKey.Create("second").Value;
        var firstMissing = CredentialErrors.NotFound(first);
        var secondMissing = CredentialErrors.NotFound(second);
        var firstRead = CredentialResult<Secret>.Failure(firstMissing);
        var secondRead = CredentialResult<Secret>.Failure(secondMissing);
        store.Read(first, Arg.Any<CancellationToken>()).Returns(firstRead);
        store.Read(second, Arg.Any<CancellationToken>()).Returns(secondRead);
        var success = CredentialResult.Success();
        store.Store(Arg.Any<CredentialKey>(), Arg.Any<Secret>(), Arg.Any<CancellationToken>()).Returns(success);
        var unavailableError = CredentialErrors.StoreUnavailable("locked");
        var unavailable = CredentialResult.Failure(unavailableError);
        store.Delete(second, Arg.Any<CancellationToken>()).Returns(unavailable);
        store.Delete(first, Arg.Any<CancellationToken>()).Returns(success);
        using var transaction = new ImportSecretTransaction(store);
        CredentialKey[] keys = [first, second];
        await transaction.CaptureAsync(keys, TestContext.CancellationToken);
        using var replacement = Secret.FromString("replacement");
        await transaction.Store(first, replacement, TestContext.CancellationToken);
        await transaction.Store(second, replacement, TestContext.CancellationToken);

        var restored = await transaction.RollbackAsync(TestContext.CancellationToken);

        restored.Should().BeFalse();
        await store.Received(1).Delete(first, Arg.Any<CancellationToken>());
    }
}
