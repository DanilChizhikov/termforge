using DTech.Core.Connections;
using DTech.Core.Files;
using DTech.Core.Files.Remote;
using DTech.Core.Safety;
using DTech.Ssh.IntegrationTests.Harness;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace DTech.Ssh.IntegrationTests;

[TestClass]
public sealed class RemoteFileBrowsingTests
{
    private const string RootOnlyDirectory = "/root";

    private static readonly byte[] SmallPayload = "payload"u8.ToArray();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task GetHomeDirectory_ReturnsLoginDirectory()
    {
        await using var session = await RemoteSession.OpenAsync();

        var home = await session.Client.GetHomeDirectoryAsync(TestContext.CancellationToken);

        home.IsSuccess.Should().BeTrue(home.Error?.Message);
        home.Value.Should().Be(SshServerContainer.HomeDirectory);
    }

    [TestMethod]
    public async Task CreateDirectory_ThenStat_ReturnsDirectoryWithPermissions()
    {
        await using var session = await RemoteSession.OpenAsync();
        var path = $"{SshServerContainer.HomeDirectory}/created-{Guid.NewGuid():N}";

        var created = await session.Client.CreateDirectoryAsync(path, TestContext.CancellationToken);
        var stat = await session.Client.StatAsync(path, TestContext.CancellationToken);

        created.IsSuccess.Should().BeTrue(created.Error?.Message);
        stat.IsSuccess.Should().BeTrue(stat.Error?.Message);
        stat.Value.Kind.Should().Be(RemoteFileKind.Directory);
        stat.Value.Permissions.Should().MatchRegex("^[r-][w-][x-][r-][w-][x-][r-][w-][x-]$");
        stat.Value.Permissions!.Substring(0, 3).Should().Be("rwx");
    }

    [TestMethod]
    public async Task CreateDirectory_Existing_ReturnsRemotePathExists()
    {
        await using var session = await RemoteSession.OpenAsync();
        var path = await session.CreateWorkDirectoryAsync(TestContext.CancellationToken);

        var result = await session.Client.CreateDirectoryAsync(path, TestContext.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.RemotePathExists);
    }

    [TestMethod]
    public async Task CreateDirectory_InReadOnlyLocation_ReturnsPermissionDenied()
    {
        await using var session = await RemoteSession.OpenAsync();

        var result = await session.Client.CreateDirectoryAsync($"/etc/termforge-{Guid.NewGuid():N}", TestContext.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.PermissionDenied);
    }

    [TestMethod]
    public async Task Stat_Missing_ReturnsRemotePathNotFound()
    {
        await using var session = await RemoteSession.OpenAsync();

        var result = await session.Client.StatAsync($"{SshServerContainer.HomeDirectory}/missing-{Guid.NewGuid():N}", TestContext.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.RemotePathNotFound);
    }

    [TestMethod]
    public async Task Rename_File_MovesIt()
    {
        await using var session = await RemoteSession.OpenAsync();
        var directory = await session.CreateWorkDirectoryAsync(TestContext.CancellationToken);
        var source = await session.UploadAsync(directory, "old.txt", TestContext.CancellationToken);
        var request = new RemoteRenameRequest(source, $"{directory}/new.txt");

        var renamed = await session.Client.RenameAsync(request, TestContext.CancellationToken);
        var listed = await session.Client.ListAsync(directory, TestContext.CancellationToken);

        renamed.IsSuccess.Should().BeTrue(renamed.Error?.Message);
        listed.Value.Select(entry => entry.Name).Should().BeEquivalentTo("new.txt");
    }

    [TestMethod]
    public async Task Rename_ToExistingName_ReturnsRemotePathExistsAndKeepsBoth()
    {
        await using var session = await RemoteSession.OpenAsync();
        var directory = await session.CreateWorkDirectoryAsync(TestContext.CancellationToken);
        var first = await session.UploadAsync(directory, "a.txt", TestContext.CancellationToken);
        var second = await session.UploadAsync(directory, "b.txt", TestContext.CancellationToken);
        var request = new RemoteRenameRequest(first, second);

        var result = await session.Client.RenameAsync(request, TestContext.CancellationToken);
        var listed = await session.Client.ListAsync(directory, TestContext.CancellationToken);

        result.Error!.Kind.Should().Be(SshErrorKind.RemotePathExists);
        listed.Value.Select(entry => entry.Name).Should().BeEquivalentTo("a.txt", "b.txt");
    }

    [TestMethod]
    public async Task Delete_File_RemovesIt()
    {
        await using var session = await RemoteSession.OpenAsync();
        var directory = await session.CreateWorkDirectoryAsync(TestContext.CancellationToken);
        var file = await session.UploadAsync(directory, "doomed.txt", TestContext.CancellationToken);

        var deleted = await session.Client.DeleteAsync(file, TestContext.CancellationToken);
        var stat = await session.Client.StatAsync(file, TestContext.CancellationToken);

        deleted.IsSuccess.Should().BeTrue(deleted.Error?.Message);
        stat.Error!.Kind.Should().Be(SshErrorKind.RemotePathNotFound);
    }

    [TestMethod]
    public async Task Delete_EmptyDirectory_RemovesIt()
    {
        await using var session = await RemoteSession.OpenAsync();
        var directory = await session.CreateWorkDirectoryAsync(TestContext.CancellationToken);

        var deleted = await session.Client.DeleteAsync(directory, TestContext.CancellationToken);
        var stat = await session.Client.StatAsync(directory, TestContext.CancellationToken);

        deleted.IsSuccess.Should().BeTrue(deleted.Error?.Message);
        stat.Error!.Kind.Should().Be(SshErrorKind.RemotePathNotFound);
    }

    [TestMethod]
    public async Task Delete_NonEmptyDirectory_ReturnsDirectoryNotEmptyAndKeepsContent()
    {
        await using var session = await RemoteSession.OpenAsync();
        var directory = await session.CreateWorkDirectoryAsync(TestContext.CancellationToken);
        var file = await session.UploadAsync(directory, "inner.txt", TestContext.CancellationToken);

        var deleted = await session.Client.DeleteAsync(directory, TestContext.CancellationToken);
        var stat = await session.Client.StatAsync(file, TestContext.CancellationToken);

        deleted.Error!.Kind.Should().Be(SshErrorKind.DirectoryNotEmpty);
        stat.IsSuccess.Should().BeTrue();
    }

    [TestMethod]
    public async Task Delete_Missing_ReturnsRemotePathNotFound()
    {
        await using var session = await RemoteSession.OpenAsync();

        var result = await session.Client.DeleteAsync($"{SshServerContainer.HomeDirectory}/missing-{Guid.NewGuid():N}", TestContext.CancellationToken);

        result.Error!.Kind.Should().Be(SshErrorKind.RemotePathNotFound);
    }

    [TestMethod]
    public async Task Delete_InReadOnlyLocation_ReturnsPermissionDenied()
    {
        await using var session = await RemoteSession.OpenAsync();

        var result = await session.Client.DeleteAsync("/etc/passwd", TestContext.CancellationToken);

        result.Error!.Kind.Should().Be(SshErrorKind.PermissionDenied);
    }

    [TestMethod]
    public async Task List_SymbolicLinks_ReportTargetKindAndBrokenLinks()
    {
        await using var session = await RemoteSession.OpenAsync();
        var directory = await session.CreateWorkDirectoryAsync(TestContext.CancellationToken);
        var file = await session.UploadAsync(directory, "target.txt", TestContext.CancellationToken);
        await session.CreateSymbolicLinkAsync($"{directory}/to-dir", "/etc");
        await session.CreateSymbolicLinkAsync($"{directory}/to-file", file);
        await session.CreateSymbolicLinkAsync($"{directory}/dangling", $"{directory}/nowhere");

        var listed = await session.Client.ListAsync(directory, TestContext.CancellationToken);

        listed.IsSuccess.Should().BeTrue(listed.Error?.Message);
        listed.Value.Should().Contain(entry => entry.Name == "to-dir" && entry.Kind == RemoteFileKind.SymbolicLink && entry.LinkTargetKind == RemoteFileKind.Directory);
        listed.Value.Should().Contain(entry => entry.Name == "to-file" && entry.Kind == RemoteFileKind.SymbolicLink && entry.LinkTargetKind == RemoteFileKind.File);
        listed.Value.Should().Contain(entry => entry.Name == "dangling" && entry.Kind == RemoteFileKind.SymbolicLink && entry.IsBrokenLink);
    }

    [TestMethod]
    public async Task Stat_SymbolicLink_DescribesTheLinkItself()
    {
        await using var session = await RemoteSession.OpenAsync();
        var directory = await session.CreateWorkDirectoryAsync(TestContext.CancellationToken);
        var linkPath = $"{directory}/to-etc";
        await session.CreateSymbolicLinkAsync(linkPath, "/etc");

        var stat = await session.Client.StatAsync(linkPath, TestContext.CancellationToken);

        stat.IsSuccess.Should().BeTrue(stat.Error?.Message);
        stat.Value.Kind.Should().Be(RemoteFileKind.SymbolicLink);
        stat.Value.LinkTargetKind.Should().Be(RemoteFileKind.Directory);
    }

    [TestMethod]
    public async Task Delete_SymbolicLinkToDirectory_IsRefusedAndKeepsTarget()
    {
        await using var session = await RemoteSession.OpenAsync();
        var directory = await session.CreateWorkDirectoryAsync(TestContext.CancellationToken);
        var realDirectory = await session.CreateWorkDirectoryAsync(TestContext.CancellationToken);
        var inner = await session.UploadAsync(realDirectory, "inner.txt", TestContext.CancellationToken);
        var linkPath = $"{directory}/link";
        await session.CreateSymbolicLinkAsync(linkPath, realDirectory);

        var deleted = await session.Client.DeleteAsync(linkPath, TestContext.CancellationToken);
        var linkStat = await session.Client.StatAsync(linkPath, TestContext.CancellationToken);
        var innerStat = await session.Client.StatAsync(inner, TestContext.CancellationToken);

        deleted.IsSuccess.Should().BeFalse();
        deleted.Error!.Kind.Should().Be(SshErrorKind.OperationFailed);
        linkStat.IsSuccess.Should().BeTrue();
        innerStat.IsSuccess.Should().BeTrue();
    }

    [TestMethod]
    public async Task Rename_SymbolicLink_IsRefusedAndKeepsTarget()
    {
        await using var session = await RemoteSession.OpenAsync();
        var directory = await session.CreateWorkDirectoryAsync(TestContext.CancellationToken);
        var realDirectory = await session.CreateWorkDirectoryAsync(TestContext.CancellationToken);
        var linkPath = $"{directory}/link";
        await session.CreateSymbolicLinkAsync(linkPath, realDirectory);
        var request = new RemoteRenameRequest(linkPath, $"{directory}/renamed");

        var renamed = await session.Client.RenameAsync(request, TestContext.CancellationToken);
        var targetStat = await session.Client.StatAsync(realDirectory, TestContext.CancellationToken);

        renamed.Error!.Kind.Should().Be(SshErrorKind.OperationFailed);
        targetStat.IsSuccess.Should().BeTrue();
    }

    [TestMethod]
    public async Task Browser_ListWithoutPath_ReturnsHomeDirectoryListing()
    {
        await using var session = await RemoteSession.OpenAsync();
        var browser = session.CreateBrowser();
        var created = await session.CreateWorkDirectoryAsync(TestContext.CancellationToken);

        var result = await browser.ListAsync(RemoteSession.ConnectionId, null, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Path.Should().Be(SshServerContainer.HomeDirectory);
        result.Value.ParentPath.Should().Be("/home");
        result.Value.Entries.Should().Contain(entry => entry.Path == created && entry.Kind == FileEntryKind.Directory);
    }

    [TestMethod]
    public async Task Browser_ListRootOnlyDirectory_ReturnsPermissionDenied()
    {
        await using var session = await RemoteSession.OpenAsync();
        var browser = session.CreateBrowser();

        var result = await browser.ListAsync(RemoteSession.ConnectionId, RootOnlyDirectory, TestContext.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(FileErrorCode.PermissionDenied);
    }

    [TestMethod]
    public async Task Browser_ListMissingPath_ReturnsNotFound()
    {
        await using var session = await RemoteSession.OpenAsync();
        var browser = session.CreateBrowser();

        var result = await browser.ListAsync(RemoteSession.ConnectionId, "/missing-directory", TestContext.CancellationToken);

        result.Error!.Code.Should().Be(FileErrorCode.NotFound);
    }

    [TestMethod]
    public async Task Browser_ListRegularFile_ReturnsNotADirectory()
    {
        await using var session = await RemoteSession.OpenAsync();
        var browser = session.CreateBrowser();

        var result = await browser.ListAsync(RemoteSession.ConnectionId, "/etc/passwd", TestContext.CancellationToken);

        result.Error!.Code.Should().Be(FileErrorCode.NotADirectory);
    }

    [TestMethod]
    public async Task Operations_DeleteWithoutConfirmation_KeepsFile()
    {
        await using var session = await RemoteSession.OpenAsync();
        var operations = session.CreateOperations();
        var directory = await session.CreateWorkDirectoryAsync(TestContext.CancellationToken);
        var file = await session.UploadAsync(directory, "keep.txt", TestContext.CancellationToken);
        var request = new RemoteDeleteRequest(file, null);

        var result = await operations.DeleteAsync(RemoteSession.ConnectionId, request, TestContext.CancellationToken);
        var stat = await session.Client.StatAsync(file, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(FileErrorCode.ConfirmationRequired);
        stat.IsSuccess.Should().BeTrue();
    }

    [TestMethod]
    public async Task Operations_DeleteWithConfirmation_RemovesFile()
    {
        await using var session = await RemoteSession.OpenAsync();
        var operations = session.CreateOperations();
        var directory = await session.CreateWorkDirectoryAsync(TestContext.CancellationToken);
        var file = await session.UploadAsync(directory, "gone.txt", TestContext.CancellationToken);
        var scope = new ConfirmationScope(DangerousOperation.RemoteDelete, RemoteSession.ConnectionId, file);
        var grant = session.Confirmations.Issue(scope);
        var request = new RemoteDeleteRequest(file, grant.Token);

        var result = await operations.DeleteAsync(RemoteSession.ConnectionId, request, TestContext.CancellationToken);
        var stat = await session.Client.StatAsync(file, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        stat.Error!.Kind.Should().Be(SshErrorKind.RemotePathNotFound);
    }

    [TestMethod]
    public async Task Operations_RenameAndCreateDirectory_WorkThroughServiceLayer()
    {
        await using var session = await RemoteSession.OpenAsync();
        var operations = session.CreateOperations();
        var directory = await session.CreateWorkDirectoryAsync(TestContext.CancellationToken);
        var file = await session.UploadAsync(directory, "before.txt", TestContext.CancellationToken);
        var rename = new RenameRequest(file, "after.txt");
        var create = new CreateDirectoryRequest(directory, "sub");

        var renamed = await operations.RenameAsync(RemoteSession.ConnectionId, rename, TestContext.CancellationToken);
        var createdResult = await operations.CreateDirectoryAsync(RemoteSession.ConnectionId, create, TestContext.CancellationToken);
        var listed = await session.Client.ListAsync(directory, TestContext.CancellationToken);

        renamed.IsSuccess.Should().BeTrue(renamed.Error?.Message);
        createdResult.IsSuccess.Should().BeTrue(createdResult.Error?.Message);
        listed.Value.Select(entry => entry.Name).Should().BeEquivalentTo("after.txt", "sub");
    }

    private sealed class RemoteSession : IAsyncDisposable
    {
        public static readonly Guid ConnectionId = Guid.NewGuid();

        private readonly ServiceProvider _services;
        private readonly SshServerCluster _cluster;

        private RemoteSession(ServiceProvider services, SshServerCluster cluster, IRemoteFileClient client)
        {
            _services = services;
            _cluster = cluster;
            Client = client;
        }

        public IRemoteFileClient Client { get; }

        public static async Task<RemoteSession> OpenAsync()
        {
            var cluster = await SshTestEnvironment.RequireClusterAsync();
            var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey);
            var services = SshTransportTestServices.Create(verifier);
            var factory = services.GetRequiredService<IRemoteFileClientFactory>();
            var target = cluster.Target.CreateKeyTarget();
            var connected = await factory.ConnectAsync(target, CancellationToken.None);
            connected.IsSuccess.Should().BeTrue(connected.Error?.Message);
            return new RemoteSession(services, cluster, connected.Value);
        }

        public async Task<string> CreateWorkDirectoryAsync(CancellationToken cancellationToken)
        {
            var path = $"{SshServerContainer.HomeDirectory}/browse-{Guid.NewGuid():N}";
            var created = await Client.CreateDirectoryAsync(path, cancellationToken);
            created.IsSuccess.Should().BeTrue(created.Error?.Message);
            return path;
        }

        public async Task<string> UploadAsync(string directory, string name, CancellationToken cancellationToken)
        {
            var path = $"{directory}/{name}";
            using var source = new MemoryStream(SmallPayload);
            var uploaded = await Client.UploadAsync(source, path, cancellationToken);
            uploaded.IsSuccess.Should().BeTrue(uploaded.Error?.Message);
            return path;
        }

        public async Task CreateSymbolicLinkAsync(string linkPath, string target)
        {
            var command = $"ln -s '{target}' '{linkPath}' && chown -h {SshServerContainer.UserName} '{linkPath}'";
            await _cluster.Target.ExecuteAsync(command, CancellationToken.None);
        }

        public ConfirmationTokenStore Confirmations { get; } = new(TimeProvider.System);

        public RemoteFileBrowser CreateBrowser()
        {
            var opener = new ConnectedClientOpener(Client);
            var normalizer = new RemotePathNormalizer();
            return new RemoteFileBrowser(opener, normalizer);
        }

        public RemoteFileOperations CreateOperations()
        {
            var opener = new ConnectedClientOpener(Client);
            var normalizer = new RemotePathNormalizer();
            var accessModes = Substitute.For<IServerAccessModeReader>();
            var writeGate = new ServerWriteGate(accessModes, Confirmations);
            return new RemoteFileOperations(opener, normalizer, writeGate);
        }

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync();
            await _services.DisposeAsync();
        }
    }

    private sealed class ConnectedClientOpener(IRemoteFileClient client) : IRemoteConnectionOpener
    {
        public Task<FileResult<IRemoteFileClient>> OpenAsync(Guid connectionId, CancellationToken cancellationToken)
        {
            var shared = new NonDisposingClient(client);
            var result = FileResult<IRemoteFileClient>.Success(shared);
            return Task.FromResult(result);
        }
    }

    private sealed class NonDisposingClient(IRemoteFileClient inner) : IRemoteFileClient
    {
        public Task<SshResult<IReadOnlyList<RemoteFileEntry>>> ListAsync(string remotePath, CancellationToken cancellationToken)
        {
            return inner.ListAsync(remotePath, cancellationToken);
        }

        public Task<SshResult<RemoteFileEntry>> StatAsync(string remotePath, CancellationToken cancellationToken)
        {
            return inner.StatAsync(remotePath, cancellationToken);
        }

        public Task<SshResult<string>> GetHomeDirectoryAsync(CancellationToken cancellationToken)
        {
            return inner.GetHomeDirectoryAsync(cancellationToken);
        }

        public Task<SshResult> CreateDirectoryAsync(string remotePath, CancellationToken cancellationToken)
        {
            return inner.CreateDirectoryAsync(remotePath, cancellationToken);
        }

        public Task<SshResult> RenameAsync(RemoteRenameRequest request, CancellationToken cancellationToken)
        {
            return inner.RenameAsync(request, cancellationToken);
        }

        public Task<SshResult> DeleteAsync(string remotePath, CancellationToken cancellationToken)
        {
            return inner.DeleteAsync(remotePath, cancellationToken);
        }

        public Task<SshResult> UploadAsync(Stream source, string remotePath, CancellationToken cancellationToken)
        {
            return inner.UploadAsync(source, remotePath, cancellationToken);
        }

        public Task<SshResult> DownloadAsync(string remotePath, Stream destination, CancellationToken cancellationToken)
        {
            return inner.DownloadAsync(remotePath, destination, cancellationToken);
        }

        public Task<SshResult> WriteFileAsync(RemoteWriteRequest request, CancellationToken cancellationToken)
        {
            return inner.WriteFileAsync(request, cancellationToken);
        }

        public Task<SshResult> ReadFileAsync(RemoteReadRequest request, CancellationToken cancellationToken)
        {
            return inner.ReadFileAsync(request, cancellationToken);
        }

        public Task<SshResult> ReplaceAsync(RemoteRenameRequest request, CancellationToken cancellationToken)
        {
            return inner.ReplaceAsync(request, cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
