using DTech.Core.Credentials;
using DTech.Credentials.Native;
using DTech.Credentials.Tests.Support;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace DTech.Credentials.Tests.Native;

[TestClass]
[DoNotParallelize]
public sealed class OperatingSystemCredentialStoreTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RoundTrip_OnCurrentPlatform_StoresReadsReplacesAndDeletesSecret()
    {
        var store = OperatingSystemCredentialStoreFactory.Create();
        await SkipWhenUnavailable(store);
        var key = TestKeys.CreateUnique();
        using var firstSecret = Secret.FromString("first-value-é中");
        using var secondSecret = Secret.FromString("second-value");

        try
        {
            var storeResult = await store.Store(key, firstSecret, TestContext.CancellationToken);
            var firstRead = await store.Read(key, TestContext.CancellationToken);
            var replaceResult = await store.Store(key, secondSecret, TestContext.CancellationToken);
            var secondRead = await store.Read(key, TestContext.CancellationToken);
            var deleteResult = await store.Delete(key, TestContext.CancellationToken);
            var readAfterDelete = await store.Read(key, TestContext.CancellationToken);
            var deleteAgain = await store.Delete(key, TestContext.CancellationToken);

            storeResult.IsSuccess.Should().BeTrue(storeResult.Error?.Message);
            using var firstValue = firstRead.Value;
            firstValue.ContentEquals(firstSecret).Should().BeTrue();
            replaceResult.IsSuccess.Should().BeTrue(replaceResult.Error?.Message);
            using var secondValue = secondRead.Value;
            secondValue.RevealString().Should().Be("second-value");
            deleteResult.IsSuccess.Should().BeTrue(deleteResult.Error?.Message);
            readAfterDelete.Error!.Code.Should().Be(CredentialErrorCode.NotFound);
            deleteAgain.Error!.Code.Should().Be(CredentialErrorCode.NotFound);
        }
        finally
        {
            await store.Delete(key, CancellationToken.None);
        }
    }

    [TestMethod]
    public async Task Read_OnCurrentPlatform_WithUnknownKey_ReturnsNotFound()
    {
        var store = OperatingSystemCredentialStoreFactory.Create();
        await SkipWhenUnavailable(store);
        var key = TestKeys.CreateUnique();

        var result = await store.Read(key, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(CredentialErrorCode.NotFound);
    }

    [TestMethod]
    public async Task Probe_OnCurrentPlatform_ReturnsActionableDiagnostic()
    {
        var store = OperatingSystemCredentialStoreFactory.Create();
        var options = CreateOptions();
        var probe = new OperatingSystemCredentialStoreProbe(store, options);

        var availability = await probe.Probe(TestContext.CancellationToken);

        TestContext.WriteLine($"{availability.StoreName}: available={availability.IsAvailable}; {availability.Diagnostic}");
        availability.StoreName.Should().Be(store.Name);
        availability.Diagnostic.Should().NotBeNullOrWhiteSpace();
    }

    private async Task SkipWhenUnavailable(IOperatingSystemCredentialStore store)
    {
        var options = CreateOptions();
        var probe = new OperatingSystemCredentialStoreProbe(store, options);
        var availability = await probe.Probe(TestContext.CancellationToken);
        if (!availability.IsAvailable)
        {
            Assert.Inconclusive($"{store.Name} is not usable in this environment: {availability.Diagnostic}");
        }
    }

    private static IOptions<CredentialStoreOptions> CreateOptions()
    {
        var storeOptions = new CredentialStoreOptions
        {
            ProbeTimeout = TimeSpan.FromSeconds(30),
        };
        return Options.Create(storeOptions);
    }
}
