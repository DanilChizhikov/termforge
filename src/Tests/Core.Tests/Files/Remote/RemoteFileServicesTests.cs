using DTech.Core.Connections;
using DTech.Core.Files;
using DTech.Core.Files.Remote;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Files.Remote;

[TestClass]
public sealed class RemoteFileServicesTests
{
    private static readonly Guid ConnectionId = Guid.NewGuid();

    private IRemoteConnectionOpener _opener = null!;
    private IRemoteFileClient _client = null!;
    private RemoteFileOperations _operations = null!;
    private RemoteFileBrowser _browser = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _client = Substitute.For<IRemoteFileClient>();
        _opener = Substitute.For<IRemoteConnectionOpener>();
        var opened = FileResult<IRemoteFileClient>.Success(_client);
        _opener.OpenAsync(ConnectionId, Arg.Any<CancellationToken>()).Returns(opened);
        var normalizer = new RemotePathNormalizer();
        _operations = new RemoteFileOperations(_opener, normalizer);
        _browser = new RemoteFileBrowser(_opener, normalizer);
    }

    [TestMethod]
    public async Task Delete_WithoutConfirmation_NeverConnects()
    {
        var request = new DeleteRequest("/home/user/file", false);

        var result = await _operations.DeleteAsync(ConnectionId, request, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(FileErrorCode.ConfirmationRequired);
        await _opener.DidNotReceive().OpenAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _client.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Delete_WithConfirmation_DeletesNormalizedPath()
    {
        _client.DeleteAsync("/home/user/file", Arg.Any<CancellationToken>()).Returns(SshResult.Success());
        var request = new DeleteRequest("/home//user/./file", true);

        var result = await _operations.DeleteAsync(ConnectionId, request, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        await _client.Received(1).DeleteAsync("/home/user/file", Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Delete_TraversalPath_IsRejectedWithoutConnecting()
    {
        var request = new DeleteRequest("/home/user/../../etc/passwd", true);

        var result = await _operations.DeleteAsync(ConnectionId, request, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(FileErrorCode.Invalid);
        await _opener.DidNotReceive().OpenAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Delete_Root_IsRejected()
    {
        var request = new DeleteRequest("/", true);

        var result = await _operations.DeleteAsync(ConnectionId, request, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(FileErrorCode.Invalid);
    }

    [TestMethod]
    public async Task Delete_PermissionDeniedFromServer_IsMappedToTypedError()
    {
        var denied = new SshError(SshErrorKind.PermissionDenied, "denied");
        _client.DeleteAsync("/etc/shadow", Arg.Any<CancellationToken>()).Returns(SshResult.Failure(denied));
        var request = new DeleteRequest("/etc/shadow", true);

        var result = await _operations.DeleteAsync(ConnectionId, request, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(FileErrorCode.PermissionDenied);
    }

    [TestMethod]
    public async Task Rename_RenamesWithinSameParent()
    {
        _client.RenameAsync(Arg.Any<RemoteRenameRequest>(), Arg.Any<CancellationToken>()).Returns(SshResult.Success());
        var request = new RenameRequest("/home/user/old.txt", "new.txt");

        var result = await _operations.RenameAsync(ConnectionId, request, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var expected = new RemoteRenameRequest("/home/user/old.txt", "/home/user/new.txt");
        await _client.Received(1).RenameAsync(expected, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Rename_NewNameWithSeparator_IsRejected()
    {
        var request = new RenameRequest("/home/user/old.txt", "../../etc/new.txt");

        var result = await _operations.RenameAsync(ConnectionId, request, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(FileErrorCode.Invalid);
        await _opener.DidNotReceive().OpenAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Rename_TargetExists_ReturnsAlreadyExists()
    {
        var exists = new SshError(SshErrorKind.RemotePathExists, "exists");
        _client.RenameAsync(Arg.Any<RemoteRenameRequest>(), Arg.Any<CancellationToken>()).Returns(SshResult.Failure(exists));
        var request = new RenameRequest("/a/b", "c");

        var result = await _operations.RenameAsync(ConnectionId, request, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(FileErrorCode.AlreadyExists);
    }

    [TestMethod]
    public async Task CreateDirectory_CombinesParentAndName()
    {
        _client.CreateDirectoryAsync("/home/user/logs", Arg.Any<CancellationToken>()).Returns(SshResult.Success());
        var request = new CreateDirectoryRequest("/home/user/", "logs");

        var result = await _operations.CreateDirectoryAsync(ConnectionId, request, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }

    [TestMethod]
    public async Task CreateDirectory_UnsafeName_IsRejected()
    {
        var request = new CreateDirectoryRequest("/home/user", "a/b");

        var result = await _operations.CreateDirectoryAsync(ConnectionId, request, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(FileErrorCode.Invalid);
    }

    [TestMethod]
    public async Task Operation_WhenConnectionFails_ReturnsConnectionError()
    {
        var failed = FileResult<IRemoteFileClient>.Failure(new FileError(FileErrorCode.ConnectionFailed, "unreachable"));
        var otherId = Guid.NewGuid();
        _opener.OpenAsync(otherId, Arg.Any<CancellationToken>()).Returns(failed);
        var request = new DeleteRequest("/a/b", true);

        var result = await _operations.DeleteAsync(otherId, request, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(FileErrorCode.ConnectionFailed);
    }

    [TestMethod]
    public async Task List_WithoutPath_StartsInHomeDirectory()
    {
        _client.GetHomeDirectoryAsync(Arg.Any<CancellationToken>()).Returns(SshResult<string>.Success("/home/user"));
        var entry = new RemoteFileEntry("a.txt", "/home/user/a.txt", RemoteFileKind.File) { Size = 3, Permissions = "rw-r--r--" };
        IReadOnlyList<RemoteFileEntry> entries = [entry];
        _client.ListAsync("/home/user", Arg.Any<CancellationToken>()).Returns(SshResult<IReadOnlyList<RemoteFileEntry>>.Success(entries));

        var result = await _browser.ListAsync(ConnectionId, null, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Path.Should().Be("/home/user");
        result.Value.ParentPath.Should().Be("/home");
        result.Value.Entries.Should().ContainSingle().Which.Should().Match<FileEntry>(item => item.Name == "a.txt" && item.Kind == FileEntryKind.File && item.Permissions == "rw-r--r--");
    }

    [TestMethod]
    public async Task List_MapsSymbolicLinkDetails()
    {
        var link = new RemoteFileEntry("l", "/l", RemoteFileKind.SymbolicLink) { LinkTargetKind = RemoteFileKind.Directory };
        var broken = new RemoteFileEntry("b", "/b", RemoteFileKind.SymbolicLink) { IsBrokenLink = true };
        IReadOnlyList<RemoteFileEntry> entries = [link, broken];
        _client.ListAsync("/", Arg.Any<CancellationToken>()).Returns(SshResult<IReadOnlyList<RemoteFileEntry>>.Success(entries));

        var result = await _browser.ListAsync(ConnectionId, "/", TestContext.CancellationToken);

        result.Value.ParentPath.Should().BeNull();
        result.Value.Entries.Should().Contain(entry => entry.Name == "l" && entry.LinkTargetKind == FileEntryKind.Directory);
        result.Value.Entries.Should().Contain(entry => entry.Name == "b" && entry.IsBrokenLink);
    }

    [TestMethod]
    [DataRow(SshErrorKind.RemotePathNotFound, FileErrorCode.NotFound)]
    [DataRow(SshErrorKind.PermissionDenied, FileErrorCode.PermissionDenied)]
    [DataRow(SshErrorKind.Cancelled, FileErrorCode.Cancelled)]
    public async Task List_ServerErrors_AreMappedToTypedErrors(SshErrorKind kind, FileErrorCode expected)
    {
        var error = new SshError(kind, "server said no");
        var missing = new SshError(SshErrorKind.RemotePathNotFound, "missing");
        _client.StatAsync("/root", Arg.Any<CancellationToken>()).Returns(SshResult<RemoteFileEntry>.Failure(missing));
        _client.ListAsync("/root", Arg.Any<CancellationToken>()).Returns(SshResult<IReadOnlyList<RemoteFileEntry>>.Failure(error));

        var result = await _browser.ListAsync(ConnectionId, "/root", TestContext.CancellationToken);

        result.Error!.Code.Should().Be(expected);
    }

    [TestMethod]
    public async Task List_OnRegularFile_ReturnsNotADirectory()
    {
        var error = new SshError(SshErrorKind.OperationFailed, "failure");
        _client.ListAsync("/etc/hosts", Arg.Any<CancellationToken>()).Returns(SshResult<IReadOnlyList<RemoteFileEntry>>.Failure(error));
        var file = new RemoteFileEntry("hosts", "/etc/hosts", RemoteFileKind.File);
        _client.StatAsync("/etc/hosts", Arg.Any<CancellationToken>()).Returns(SshResult<RemoteFileEntry>.Success(file));

        var result = await _browser.ListAsync(ConnectionId, "/etc/hosts", TestContext.CancellationToken);

        result.Error!.Code.Should().Be(FileErrorCode.NotADirectory);
    }

    [TestMethod]
    public async Task List_RelativePath_IsRejectedWithoutConnecting()
    {
        var result = await _browser.ListAsync(ConnectionId, "../etc", TestContext.CancellationToken);

        result.Error!.Code.Should().Be(FileErrorCode.Invalid);
        await _opener.DidNotReceive().OpenAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
