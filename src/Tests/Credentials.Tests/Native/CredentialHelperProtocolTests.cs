using System.Buffers.Binary;
using System.Text;
using DTech.Core.Credentials;
using DTech.Credentials.Native;
using DTech.Credentials.Native.Processes;
using DTech.Credentials.Tests.Support;
using FluentAssertions;
using NSubstitute;

namespace DTech.Credentials.Tests.Native;

[TestClass]
public sealed class CredentialHelperProtocolTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Packet_RoundTripsBinarySecretAndClearsPayloadOnDispose()
    {
        byte[] bytes = [0, 1, 255, 128, 10, 13];
        var metadata = new CredentialHelperRequest(CredentialHelperOperation.Store, "connection:test:password", 1000);
        using var packet = new CredentialHelperPacket<CredentialHelperRequest>(metadata, bytes);
        using var stream = new MemoryStream();

        await CredentialHelperProtocol.WriteAsync(stream, packet, TestContext.CancellationToken);
        stream.Position = 0;
        var restored = await CredentialHelperProtocol.ReadAsync<CredentialHelperRequest>(stream, TestContext.CancellationToken);

        restored.Metadata.Should().Be(metadata);
        restored.SecretBytes.Should().Equal(bytes);
        var restoredBytes = restored.SecretBytes;
        restored.Dispose();
        restoredBytes.Should().OnlyContain(value => value == 0);
        packet.Dispose();
        bytes.Should().OnlyContain(value => value == 0);
    }

    [TestMethod]
    public async Task Read_RejectsOversizedPayloadBeforeReadingIt()
    {
        var header = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(header, 10);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), CredentialHelperProtocol.MaximumSecretBytes + 1);
        using var stream = new MemoryStream(header);

        var reading = () => CredentialHelperProtocol.ReadAsync<CredentialHelperRequest>(stream, TestContext.CancellationToken);

        await reading.Should().ThrowAsync<InvalidDataException>();
    }

    [TestMethod]
    public async Task Server_DisposesSecretPassedToNativeStore()
    {
        var raw = Substitute.For<IOperatingSystemCredentialStore>();
        var available = CredentialStoreAvailability.Available("test");
        raw.CheckPrerequisites().Returns(available);
        Secret? captured = null;
        raw.Store(Arg.Any<CredentialKey>(), Arg.Any<Secret>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            captured = call.Arg<Secret>();
            captured.RevealString().Should().Be("private-value");
            return CredentialResult.Success();
        });
        var server = new CredentialHelperServer(_ => raw);
        var metadata = new CredentialHelperRequest(CredentialHelperOperation.Store, "test-key", 1000);
        var bytes = Encoding.UTF8.GetBytes("private-value");
        using var request = new CredentialHelperPacket<CredentialHelperRequest>(metadata, bytes);
        using var input = new MemoryStream();
        using var output = new MemoryStream();
        await CredentialHelperProtocol.WriteAsync(input, request, TestContext.CancellationToken);
        input.Position = 0;

        await server.RunAsync(input, output, TestContext.CancellationToken);

        captured.Should().NotBeNull();
        var accessing = () => captured!.Length;
        accessing.Should().Throw<ObjectDisposedException>();
        output.Position = 0;
        using var response = await CredentialHelperProtocol.ReadAsync<CredentialHelperResponse>(output, TestContext.CancellationToken);
        response.Metadata.Error.Should().BeNull();
    }

    [TestMethod]
    public async Task Server_RoundTripsThroughRawStoreWithoutRecursiveProcesses()
    {
        var raw = new InMemoryOperatingSystemStore();
        var server = new CredentialHelperServer(_ => raw);
        byte[] payload = [65, 66, 67];
        using var stored = await Exchange(server, CredentialHelperOperation.Store, payload);
        using var read = await Exchange(server, CredentialHelperOperation.Read, []);
        using var deleted = await Exchange(server, CredentialHelperOperation.Delete, []);
        using var missing = await Exchange(server, CredentialHelperOperation.Read, []);

        stored.Metadata.Error.Should().BeNull();
        read.SecretBytes.Should().Equal(65, 66, 67);
        deleted.Metadata.Error.Should().BeNull();
        missing.Metadata.Error!.Code.Should().Be(CredentialErrorCode.NotFound);
        raw.StoreCallCount.Should().Be(1);
    }

    [TestMethod]
    public async Task Server_NativeFailureDisposesSecretAndDoesNotExposeExceptionText()
    {
        var raw = Substitute.For<IOperatingSystemCredentialStore>();
        var available = CredentialStoreAvailability.Available("test");
        raw.CheckPrerequisites().Returns(available);
        Secret? captured = null;
        raw.Store(Arg.Any<CredentialKey>(), Arg.Any<Secret>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            captured = call.Arg<Secret>();
            var failure = new InvalidOperationException("private-value");
            return Task.FromException<CredentialResult>(failure);
        });
        var server = new CredentialHelperServer(_ => raw);
        var bytes = Encoding.UTF8.GetBytes("private-value");

        using var response = await Exchange(server, CredentialHelperOperation.Store, bytes);

        response.Metadata.Error!.Code.Should().Be(CredentialErrorCode.StoreFailure);
        response.Metadata.Error.Message.Should().NotContain("private-value");
        var accessing = () => captured!.Length;
        accessing.Should().Throw<ObjectDisposedException>();
    }

    [TestMethod]
    public async Task Server_RejectsUnknownOperationBeforeCreatingNativeStore()
    {
        var storeCreated = false;
        var server = new CredentialHelperServer(_ =>
        {
            storeCreated = true;
            return new InMemoryOperatingSystemStore();
        });
        var operation = (CredentialHelperOperation)999;

        using var response = await Exchange(server, operation, []);

        response.Metadata.Error!.Code.Should().Be(CredentialErrorCode.StoreFailure);
        storeCreated.Should().BeFalse();
    }

    [TestMethod]
    public void LaunchInfo_ContainsOnlyApplicationPathAndHelperFlag()
    {
        var apphost = new CredentialHelperLaunchTarget("/application/TermForge", null);
        var managed = new CredentialHelperLaunchTarget("/runtime/dotnet", "/application/TermForge.dll");

        var apphostInfo = CredentialHelperProcessFactory.CreateStartInfo(apphost);
        var managedInfo = CredentialHelperProcessFactory.CreateStartInfo(managed);

        apphostInfo.ArgumentList.Should().Equal(CredentialHelperCommand.Argument);
        managedInfo.ArgumentList.Should().Equal("/application/TermForge.dll", CredentialHelperCommand.Argument);
        managedInfo.RedirectStandardInput.Should().BeTrue();
        managedInfo.RedirectStandardOutput.Should().BeTrue();
        managedInfo.UseShellExecute.Should().BeFalse();
    }

    private async Task<CredentialHelperPacket<CredentialHelperResponse>> Exchange(CredentialHelperServer server, CredentialHelperOperation operation, byte[] bytes)
    {
        var metadata = new CredentialHelperRequest(operation, "test-key", 1000);
        using var request = new CredentialHelperPacket<CredentialHelperRequest>(metadata, bytes);
        using var input = new MemoryStream();
        using var output = new MemoryStream();
        await CredentialHelperProtocol.WriteAsync(input, request, TestContext.CancellationToken);
        input.Position = 0;
        await server.RunAsync(input, output, TestContext.CancellationToken);
        output.Position = 0;
        return await CredentialHelperProtocol.ReadAsync<CredentialHelperResponse>(output, TestContext.CancellationToken);
    }
}
