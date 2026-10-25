using DTech.Core.Credentials;
using DTech.Credentials.Native;
using DTech.Credentials.Selection;
using DTech.Credentials.Tests.Support;
using DTech.Credentials.Vault;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace DTech.Credentials.Tests.Selection;

[TestClass]
public sealed class CredentialBackendSelectorTests
{
    private const string FailingStoreName = "Failing test store";
    private const string StoreFailureMessage = "keyring daemon is not running";

    private VaultTestContext _context = null!;
    private RecordingLogger<CredentialBackendSelector> _selectorLogger = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _context = new VaultTestContext();
        _selectorLogger = new RecordingLogger<CredentialBackendSelector>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Dispose();
    }

    [TestMethod]
    public async Task Select_WhenOperatingSystemStorePassesSelfTest_UsesOperatingSystemStore()
    {
        var operatingSystemStore = new InMemoryOperatingSystemStore();
        var selector = CreateSelector(operatingSystemStore);

        var selection = await selector.Select(TestContext.CancellationToken);

        selection.Backend.Should().Be(CredentialBackend.OperatingSystemStore);
        selection.Store.Should().BeSameAs(operatingSystemStore);
        selection.OperatingSystemStore.IsAvailable.Should().BeTrue();
        operatingSystemStore.Keys.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Select_WhenPrerequisitesAreMissing_FallsBackToVaultWithDiagnostic()
    {
        var operatingSystemStore = CreateFailingStore();
        var missingLibrary = CredentialStoreAvailability.Unavailable(FailingStoreName, "libsecret-1.so.0 is not installed.");
        operatingSystemStore.CheckPrerequisites().Returns(missingLibrary);
        var selector = CreateSelector(operatingSystemStore);

        var selection = await selector.Select(TestContext.CancellationToken);

        selection.Backend.Should().Be(CredentialBackend.EncryptedVault);
        selection.OperatingSystemStore.IsAvailable.Should().BeFalse();
        selection.Reason.Should().Contain("libsecret-1.so.0 is not installed.");
        _ = operatingSystemStore.DidNotReceiveWithAnyArgs().Store(default, default!, default);
    }

    [TestMethod]
    public async Task Select_WhenOperatingSystemStoreWriteFails_FallsBackToVault()
    {
        var operatingSystemStore = CreateFailingStore();
        var storeError = CredentialErrors.StoreFailure(StoreFailureMessage);
        var storeFailure = CredentialResult.Failure(storeError);
        operatingSystemStore.Store(default, default!, default).ReturnsForAnyArgs(storeFailure);
        var selector = CreateSelector(operatingSystemStore);

        var selection = await selector.Select(TestContext.CancellationToken);

        selection.Backend.Should().Be(CredentialBackend.EncryptedVault);
        selection.OperatingSystemStore.Diagnostic.Should().Contain(StoreFailureMessage);
    }

    [TestMethod]
    public async Task Select_WhenOperatingSystemStoreReturnsDifferentValue_FallsBackToVault()
    {
        var operatingSystemStore = CreateFailingStore();
        var storeSuccess = CredentialResult.Success();
        operatingSystemStore.Store(default, default!, default).ReturnsForAnyArgs(storeSuccess);
        var otherSecret = Secret.FromString("unexpected");
        var readResult = CredentialResult<Secret>.Success(otherSecret);
        operatingSystemStore.Read(default, default).ReturnsForAnyArgs(readResult);
        var selector = CreateSelector(operatingSystemStore);

        var selection = await selector.Select(TestContext.CancellationToken);

        selection.Backend.Should().Be(CredentialBackend.EncryptedVault);
        selection.OperatingSystemStore.Diagnostic.Should().Contain("different value");
    }

    [TestMethod]
    public async Task Select_WhenOperatingSystemStoreDoesNotRespond_FallsBackAfterTimeout()
    {
        var operatingSystemStore = CreateFailingStore();
        var neverCompletes = new TaskCompletionSource<CredentialResult>();
        operatingSystemStore.Store(default, default!, default).ReturnsForAnyArgs(neverCompletes.Task);
        _context.Options.Value.ProbeTimeout = TimeSpan.FromMilliseconds(200);
        var selector = CreateSelector(operatingSystemStore);

        var selection = await selector.Select(TestContext.CancellationToken);

        selection.Backend.Should().Be(CredentialBackend.EncryptedVault);
        selection.OperatingSystemStore.Diagnostic.Should().Contain("did not respond");
    }

    [TestMethod]
    public async Task Select_WhenOperatingSystemStoreThrows_FallsBackToVault()
    {
        var operatingSystemStore = CreateFailingStore();
        var nativeFailure = new DllNotFoundException("Security.framework is missing");
        operatingSystemStore.Store(default, default!, default).ThrowsAsyncForAnyArgs(nativeFailure);
        var selector = CreateSelector(operatingSystemStore);

        var selection = await selector.Select(TestContext.CancellationToken);

        selection.Backend.Should().Be(CredentialBackend.EncryptedVault);
        selection.OperatingSystemStore.Diagnostic.Should().Contain("Security.framework is missing");
    }

    [TestMethod]
    public async Task Select_WhenVaultAlreadyExists_KeepsUsingVault()
    {
        var existingVault = _context.CreateVault();
        using var masterPassword = Secret.FromString(VaultTestContext.MasterPassword);
        await existingVault.Unlock(masterPassword, TestContext.CancellationToken);
        var operatingSystemStore = new InMemoryOperatingSystemStore();
        var selector = CreateSelector(operatingSystemStore);

        var selection = await selector.Select(TestContext.CancellationToken);

        selection.Backend.Should().Be(CredentialBackend.EncryptedVault);
        selection.OperatingSystemStore.IsAvailable.Should().BeTrue();
        selection.Reason.Should().Contain("already exists");
    }

    [TestMethod]
    public async Task Select_CalledRepeatedly_ProbesOnlyOnce()
    {
        var operatingSystemStore = new InMemoryOperatingSystemStore();
        var selector = CreateSelector(operatingSystemStore);

        var first = await selector.Select(TestContext.CancellationToken);
        var second = await selector.Select(TestContext.CancellationToken);

        second.Should().BeSameAs(first);
        operatingSystemStore.StoreCallCount.Should().Be(1);
    }

    [TestMethod]
    public async Task Select_WhenFallingBack_LogsActionableWarning()
    {
        var operatingSystemStore = CreateFailingStore();
        var storeError = CredentialErrors.StoreFailure(StoreFailureMessage);
        var storeFailure = CredentialResult.Failure(storeError);
        operatingSystemStore.Store(default, default!, default).ReturnsForAnyArgs(storeFailure);
        var selector = CreateSelector(operatingSystemStore);

        await selector.Select(TestContext.CancellationToken);

        var warnings = _selectorLogger.Entries.Where(entry => entry.Level == LogLevel.Warning).ToArray();
        warnings.Should().ContainSingle();
        warnings[0].Message.Should().Contain(StoreFailureMessage).And.Contain(_context.VaultPath);
    }

    [TestMethod]
    public async Task CredentialStore_WhenVaultFallbackSelected_RequiresUnlockBeforeStoring()
    {
        var operatingSystemStore = CreateFailingStore();
        var unavailable = CredentialStoreAvailability.Unavailable(FailingStoreName, "no session bus");
        operatingSystemStore.CheckPrerequisites().Returns(unavailable);
        var vault = _context.CreateVault();
        var selector = CreateSelector(operatingSystemStore, vault);
        var store = new CredentialStore(selector);
        var gate = new CredentialVaultGate(selector, vault);
        var key = TestKeys.Create("connections/7/password");
        using var secret = Secret.FromString("fallback-secret");
        using var masterPassword = Secret.FromString(VaultTestContext.MasterPassword);

        var lockedResult = await store.Store(key, secret, TestContext.CancellationToken);
        var unlockResult = await gate.Unlock(masterPassword, TestContext.CancellationToken);
        var storeResult = await store.Store(key, secret, TestContext.CancellationToken);
        var readResult = await store.Read(key, TestContext.CancellationToken);

        lockedResult.Error!.Code.Should().Be(CredentialErrorCode.VaultLocked);
        unlockResult.IsSuccess.Should().BeTrue();
        storeResult.IsSuccess.Should().BeTrue();
        using var readSecret = readResult.Value;
        readSecret.RevealString().Should().Be("fallback-secret");
        _ = operatingSystemStore.DidNotReceive().Store(key, secret, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task CredentialStore_WhenOperatingSystemStoreSelected_DelegatesToIt()
    {
        var operatingSystemStore = new InMemoryOperatingSystemStore();
        var selector = CreateSelector(operatingSystemStore);
        var store = new CredentialStore(selector);
        var key = TestKeys.Create("connections/8/password");
        using var secret = Secret.FromString("os-secret");

        await store.Store(key, secret, TestContext.CancellationToken);

        operatingSystemStore.Keys.Should().ContainSingle().Which.Should().Be(key.Value);
        File.Exists(_context.VaultPath).Should().BeFalse();
    }

    [TestMethod]
    public async Task VaultGate_WhenOperatingSystemStoreSelected_ReturnsVaultNotInUse()
    {
        var operatingSystemStore = new InMemoryOperatingSystemStore();
        var vault = _context.CreateVault();
        var selector = CreateSelector(operatingSystemStore, vault);
        var gate = new CredentialVaultGate(selector, vault);
        using var masterPassword = Secret.FromString(VaultTestContext.MasterPassword);

        var result = await gate.Unlock(masterPassword, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(CredentialErrorCode.VaultNotInUse);
        File.Exists(_context.VaultPath).Should().BeFalse();
    }

    [TestMethod]
    public async Task StatusProvider_ReportsSelectedBackendAndVaultState()
    {
        var operatingSystemStore = CreateFailingStore();
        var unavailable = CredentialStoreAvailability.Unavailable(FailingStoreName, "no session bus");
        operatingSystemStore.CheckPrerequisites().Returns(unavailable);
        var vault = _context.CreateVault();
        var selector = CreateSelector(operatingSystemStore, vault);
        var statusProvider = new CredentialStoreStatusProvider(selector, vault);

        var status = await statusProvider.GetStatus(TestContext.CancellationToken);

        status.Backend.Should().Be(CredentialBackend.EncryptedVault);
        status.VaultState.Should().Be(CredentialVaultState.NotCreated);
        status.OperatingSystemStore.Should().Be(unavailable);
        status.SelectionReason.Should().Contain("no session bus");
    }

    private static IOperatingSystemCredentialStore CreateFailingStore()
    {
        var store = Substitute.For<IOperatingSystemCredentialStore>();
        store.Name.Returns(FailingStoreName);
        var available = CredentialStoreAvailability.Available(FailingStoreName);
        store.CheckPrerequisites().Returns(available);
        return store;
    }

    private CredentialBackendSelector CreateSelector(IOperatingSystemCredentialStore operatingSystemStore)
    {
        var vault = _context.CreateVault();
        return CreateSelector(operatingSystemStore, vault);
    }

    private CredentialBackendSelector CreateSelector(IOperatingSystemCredentialStore operatingSystemStore, EncryptedVault vault)
    {
        var probe = new OperatingSystemCredentialStoreProbe(operatingSystemStore, _context.Options);
        return new CredentialBackendSelector(operatingSystemStore, probe, vault, _selectorLogger);
    }
}
