using DTech.Core.Connections;
using DTech.HostKeys.Tests.Support;
using DTech.Persistence;
using DTech.Persistence.Database;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DTech.HostKeys.Tests.Trust;

[TestClass]
public sealed class TrustedHostKeyStoreTests
{
    private static readonly SshEndpoint Endpoint = new("Server.Example", 2222);

    private TemporaryDirectory _directory = null!;
    private ServiceProvider _services = null!;
    private ITrustedHostKeyStore _store = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _directory = new TemporaryDirectory();
        var settings = new Dictionary<string, string?>
        {
            [$"{PersistenceOptions.SectionName}:{nameof(PersistenceOptions.DatabasePath)}"] = _directory.Combine("termforge.db"),
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddPersistence(configuration);
        collection.AddHostKeyVerification(configuration);
        _services = collection.BuildServiceProvider();
        var migrator = _services.GetRequiredService<DatabaseMigrator>();
        await migrator.MigrateAsync(CancellationToken.None);
        _store = _services.GetRequiredService<ITrustedHostKeyStore>();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _services.DisposeAsync();
        _directory.Dispose();
    }

    [TestMethod]
    public async Task AddAsync_ThenGet_ReturnsKeyForSameHostIgnoringCase()
    {
        var hostKey = TestPublicKey.CreateEd25519().HostKey;
        var request = new HostKeyVerificationRequest(Endpoint, hostKey);

        await _store.AddAsync(request, CancellationToken.None);
        var lowerCaseEndpoint = new SshEndpoint("server.example", 2222);
        var trustedKeys = _store.GetTrustedHostKeys(lowerCaseEndpoint);

        trustedKeys.Should().Equal(hostKey);
    }

    [TestMethod]
    public async Task AddAsync_KeepsPortsApart()
    {
        var hostKey = TestPublicKey.CreateEd25519().HostKey;
        var request = new HostKeyVerificationRequest(Endpoint, hostKey);

        await _store.AddAsync(request, CancellationToken.None);
        var otherPortEndpoint = new SshEndpoint("server.example", 22);

        _store.GetTrustedHostKeys(otherPortEndpoint).Should().BeEmpty();
    }

    [TestMethod]
    public async Task AddAsync_Twice_StoresOnce()
    {
        var hostKey = TestPublicKey.CreateEd25519().HostKey;
        var request = new HostKeyVerificationRequest(Endpoint, hostKey);

        await _store.AddAsync(request, CancellationToken.None);
        await _store.AddAsync(request, CancellationToken.None);

        _store.GetTrustedHostKeys(Endpoint).Should().ContainSingle();
    }

    [TestMethod]
    public async Task ReplaceAsync_RemovesKeysOfSameAlgorithmOnly()
    {
        var oldEd25519 = TestPublicKey.CreateEd25519().HostKey;
        var rsa = TestPublicKey.CreateRsa().HostKey;
        var newEd25519 = TestPublicKey.CreateEd25519().HostKey;
        var oldRequest = new HostKeyVerificationRequest(Endpoint, oldEd25519);
        var rsaRequest = new HostKeyVerificationRequest(Endpoint, rsa);
        var replacement = new HostKeyVerificationRequest(Endpoint, newEd25519);
        await _store.AddAsync(oldRequest, CancellationToken.None);
        await _store.AddAsync(rsaRequest, CancellationToken.None);

        await _store.ReplaceAsync(replacement, CancellationToken.None);

        _store.GetTrustedHostKeys(Endpoint).Should().BeEquivalentTo([rsa, newEd25519]);
    }
}
