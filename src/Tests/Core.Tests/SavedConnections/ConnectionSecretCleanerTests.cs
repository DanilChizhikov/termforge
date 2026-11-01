using DTech.Core.Credentials;
using DTech.Core.Editor.Sudo;
using DTech.Core.SavedConnections;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.SavedConnections;

[TestClass]
public sealed class ConnectionSecretCleanerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Remove_WhenOneSecretFails_StillAttemptsBothAndReturnsTheFailure(bool failSudo)
    {
        var connectionId = Guid.NewGuid();
        var passwordKey = PasswordCredentialKey.For(connectionId);
        var sudoKey = SudoPasswordStore.KeyFor(connectionId);
        var failure = CredentialErrors.VaultLocked();
        var credentials = Substitute.For<ICredentialStore>();
        var cancellationToken = TestContext.CancellationToken;
        credentials.Delete(passwordKey, cancellationToken).Returns(failSudo ? CredentialResult.Success() : CredentialResult.Failure(failure));
        credentials.Delete(sudoKey, cancellationToken).Returns(failSudo ? CredentialResult.Failure(failure) : CredentialResult.Success());
        var cleaner = new ConnectionSecretCleaner(credentials);

        var error = await cleaner.RemoveAsync(connectionId, cancellationToken);

        error.Should().Be(failure);
        await credentials.Received(1).Delete(passwordKey, cancellationToken);
        await credentials.Received(1).Delete(sudoKey, cancellationToken);
    }
}
