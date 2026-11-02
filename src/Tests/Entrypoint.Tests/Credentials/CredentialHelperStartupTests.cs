using DTech.Core.Credentials;
using DTech.Credentials;
using DTech.Credentials.Native.Processes;
using DTech.Credentials.Native;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.Tests.Credentials;

[TestClass]
public sealed class CredentialHelperStartupTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ManagedApplication_HelperDispatchesBeforeStartingWebHost()
    {
        var settings = new CredentialStoreOptions();
        var options = Options.Create(settings);
        var factory = new CredentialHelperProcessFactory();
        var runner = new CredentialHelperRunner(factory, options);
        var metadata = new CredentialHelperRequest(CredentialHelperOperation.Read, "invalid key", 10000);
        using var request = new CredentialHelperPacket<CredentialHelperRequest>(metadata, []);

        using var response = await runner.RunAsync(request, TestContext.CancellationToken);

        response.Metadata.Error!.Code.Should().Be(CredentialErrorCode.InvalidKey);
    }

    [TestMethod]
    public async Task ManagedApplication_HelperSupportsNativeCredentialRoundTrip()
    {
        var settings = new CredentialStoreOptions();
        var options = Options.Create(settings);
        var factory = new CredentialHelperProcessFactory();
        var runner = new CredentialHelperRunner(factory, options);
        var raw = OperatingSystemCredentialStoreFactory.Create();
        var store = new ProcessCredentialStore(raw, runner);
        var probe = new OperatingSystemCredentialStoreProbe(store, options);

        var availability = await probe.Probe(TestContext.CancellationToken);

        if (!availability.IsAvailable)
        {
            Assert.Inconclusive(availability.Diagnostic);
        }

        availability.IsAvailable.Should().BeTrue();
    }
}
