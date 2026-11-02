using DTech.Core.Connections;
using DTech.Core.Files;
using DTech.Core.Files.Search;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Files.Search;

[TestClass]
public sealed class SftpFileTreeReaderTests
{
    [TestMethod]
    public async Task List_RequestsEntriesWithoutResolvingSymbolicLinkTargets()
    {
        var client = Substitute.For<IRemoteFileClient>();
        RemoteFileEntry[] entries = [new("link", "/data/link", RemoteFileKind.SymbolicLink)];
        var result = SshResult<IReadOnlyList<RemoteFileEntry>>.Success(entries);
        client.ListAsync("/data", false, Arg.Any<CancellationToken>()).Returns(result);
        await using var reader = new SftpFileTreeReader(client);

        var listed = await reader.ListAsync("/data", CancellationToken.None);

        listed.IsSuccess.Should().BeTrue(listed.Error?.Message);
        var entry = listed.Value.Should().ContainSingle().Subject;
        entry.Kind.Should().Be(FileEntryKind.SymbolicLink);
        entry.LinkTargetKind.Should().BeNull();
        await client.Received(1).ListAsync("/data", false, CancellationToken.None);
        await client.DidNotReceiveWithAnyArgs().ListAsync(default!, default(CancellationToken));
    }
}
