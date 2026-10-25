using DTech.Core.Credentials;
using DTech.Credentials.Native;
using DTech.Credentials.Tests.Support;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace DTech.Credentials.Tests.Native;

[TestClass]
public sealed class OperatingSystemCredentialStoreProbeTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Probe_StoresPrintableTextSecretAcceptedByTextOnlyStores()
    {
        var store = new InMemoryOperatingSystemStore();
        var storeOptions = new CredentialStoreOptions();
        var options = Options.Create(storeOptions);
        var probe = new OperatingSystemCredentialStoreProbe(store, options);

        for (var attempt = 0; attempt < 20; attempt++)
        {
            await probe.Probe(TestContext.CancellationToken);

            var probeBytes = store.LastStoredValue;
            probeBytes.Should().NotBeEmpty();
            probeBytes.Should().OnlyContain(value => char.IsAsciiHexDigit((char)value));
        }
    }

    [TestMethod]
    public async Task Probe_RemovesProbeSecretAfterSelfTest()
    {
        var store = new InMemoryOperatingSystemStore();
        var storeOptions = new CredentialStoreOptions();
        var options = Options.Create(storeOptions);
        var probe = new OperatingSystemCredentialStoreProbe(store, options);

        var availability = await probe.Probe(TestContext.CancellationToken);

        availability.IsAvailable.Should().BeTrue();
        store.Keys.Should().BeEmpty();
    }
}
