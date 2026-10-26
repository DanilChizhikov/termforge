using System.Text;
using DTech.Core.Editor;
using FluentAssertions;

namespace DTech.Core.Tests.Editor;

[TestClass]
public sealed class RemoteFileSaverTests
{
    private const string Directory = "/srv/app";
    private const string FilePath = "/srv/app/config.yml";
    private const string Original = "port: 80\n";
    private const string Edited = "port: 8080\n";
    private const int ExecutableMode = 0b111_101_000;

    private static readonly DateTimeOffset Start = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private InMemoryEditorFileClient _client = null!;
    private EditorGraph _graph = null!;
    private RemoteFileLocation _location = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _client = new InMemoryEditorFileClient();
        var timeProvider = new SteppingTimeProvider(Start);
        _graph = EditorGraph.CreateDefault(timeProvider);
        _location = _graph.Paths.Resolve(FilePath).Value;
        _client.Seed(FilePath, Original, ExecutableMode);
    }

    [TestMethod]
    public async Task Save_MatchingVersion_ReplacesContentAtomicallyAndKeepsBackup()
    {
        var version = await OpenVersionAsync();

        var result = await SaveAsync(Edited, version);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.IsSaved.Should().BeTrue();
        var saved = result.Value.Saved!;
        saved.Strategy.Should().Be(SaveStrategy.AtomicRename);
        saved.Backup.Should().NotBeNull();
        _client.ReadText(FilePath).Should().Be(Edited);
        _client.ReadText(saved.Backup!.Path).Should().Be(Original);
        saved.Version.Size.Should().Be(Encoding.UTF8.GetByteCount(Edited));
        AuxiliaryFilesExceptBackups().Should().BeEmpty();
    }

    [TestMethod]
    public async Task Save_NewVersionCanBeUsedForNextSave()
    {
        var version = await OpenVersionAsync();
        var first = await SaveAsync(Edited, version);

        var second = await SaveAsync(Original, first.Value.Saved!.Version);

        second.Value.IsSaved.Should().BeTrue();
        _client.ReadText(FilePath).Should().Be(Original);
    }

    [TestMethod]
    public async Task Save_ExternallyModified_ReturnsConflictWithCurrentContentAndKeepsFile()
    {
        var version = await OpenVersionAsync();
        _client.Seed(FilePath, "port: 443\n", ExecutableMode);

        var result = await SaveAsync(Edited, version);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.IsSaved.Should().BeFalse();
        var conflict = result.Value.Conflict!;
        conflict.Reason.Should().Be(ConflictDetector.ChangedReason);
        conflict.CurrentContent!.Value.Text.Should().Be("port: 443\n");
        conflict.CurrentVersion.Should().NotBe(version);
        _client.ReadText(FilePath).Should().Be("port: 443\n");
        _client.NamesIn(Directory).Should().ContainSingle();
    }

    [TestMethod]
    public async Task Save_SameSizeAndTimeButDifferentContent_IsDetectedByHash()
    {
        var version = await OpenVersionAsync();
        var tampered = version with { Sha256 = new string('0', 64) };

        var result = await SaveAsync(Edited, tampered);

        result.Value.IsSaved.Should().BeFalse();
        _client.ReadText(FilePath).Should().Be(Original);
    }

    [TestMethod]
    public async Task Save_FileDeletedAfterOpen_ReturnsDeletedConflict()
    {
        var version = await OpenVersionAsync();
        await _client.DeleteFileAsync(FilePath, TestContext.CancellationToken);

        var result = await SaveAsync(Edited, version);

        result.Value.Conflict!.Reason.Should().Be(ConflictDetector.DeletedReason);
        _client.Exists(FilePath).Should().BeFalse();
    }

    [TestMethod]
    public async Task Save_ExistingFileWithoutExpectedVersion_ReturnsCreatedConflict()
    {
        var result = await SaveAsync(Edited, null);

        result.Value.Conflict!.Reason.Should().Be(ConflictDetector.CreatedReason);
        _client.ReadText(FilePath).Should().Be(Original);
    }

    [TestMethod]
    public async Task Save_NewFile_CreatesPrivateFileWithoutBackup()
    {
        await _client.DeleteFileAsync(FilePath, TestContext.CancellationToken);

        var result = await SaveAsync(Edited, null);

        result.Value.Saved!.Backup.Should().BeNull();
        _client.ReadText(FilePath).Should().Be(Edited);
        _client.ModeOf(FilePath).Should().Be(FileReplacer.NewFileMode);
    }

    [TestMethod]
    public async Task Save_PreservesPermissionsAndOwner()
    {
        var version = await OpenVersionAsync();

        await SaveAsync(Edited, version);

        _client.ModeOf(FilePath).Should().Be(ExecutableMode);
        _client.UserIdOf(FilePath).Should().Be(InMemoryEditorFileClient.OwnerId);
    }

    [TestMethod]
    public async Task Save_OwnerCannotBeKept_WritesInPlaceWithWarning()
    {
        var version = await OpenVersionAsync();
        _client.FailChangeOwner = true;

        var result = await SaveAsync(Edited, version);

        var saved = result.Value.Saved!;
        saved.Strategy.Should().Be(SaveStrategy.InPlace);
        saved.Warnings.Should().NotBeEmpty();
        _client.ReadText(FilePath).Should().Be(Edited);
        _client.UserIdOf(FilePath).Should().Be(InMemoryEditorFileClient.OwnerId);
        AuxiliaryFilesExceptBackups().Should().BeEmpty();
    }

    [TestMethod]
    public async Task Save_TemporaryWriteFails_KeepsOriginalAndReportsError()
    {
        var version = await OpenVersionAsync();
        _client.FailCreate = static path => path.EndsWith(".tmp", StringComparison.Ordinal);

        var result = await SaveAsync(Edited, version);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Contain("original file was not changed");
        _client.ReadText(FilePath).Should().Be(Original);
        AuxiliaryFilesExceptBackups().Should().BeEmpty();
    }

    [TestMethod]
    public async Task Save_BackupFails_LeavesFileUnchanged()
    {
        var version = await OpenVersionAsync();
        _client.FailCreate = static path => path.EndsWith(".bak", StringComparison.Ordinal);

        var result = await SaveAsync(Edited, version);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Contain("backup could not be created");
        _client.ReadText(FilePath).Should().Be(Original);
        _client.NamesIn(Directory).Should().ContainSingle();
    }

    [TestMethod]
    public async Task Save_AtomicRenameFails_KeepsOriginalAndRemovesTemporary()
    {
        var version = await OpenVersionAsync();
        _client.FailAtomicReplace = true;

        var result = await SaveAsync(Edited, version);

        result.IsSuccess.Should().BeFalse();
        _client.ReadText(FilePath).Should().Be(Original);
        AuxiliaryFilesExceptBackups().Should().BeEmpty();
    }

    [TestMethod]
    public async Task Save_AtomicRenameUnsupported_FallsBackToRenames()
    {
        var version = await OpenVersionAsync();
        _client.AtomicReplaceUnsupported = true;

        var result = await SaveAsync(Edited, version);

        result.Value.Saved!.Strategy.Should().Be(SaveStrategy.RenameFallback);
        _client.ReadText(FilePath).Should().Be(Edited);
        _client.ModeOf(FilePath).Should().Be(ExecutableMode);
        AuxiliaryFilesExceptBackups().Should().BeEmpty();
    }

    [TestMethod]
    public async Task Save_FallbackSwapFails_RestoresOriginal()
    {
        var version = await OpenVersionAsync();
        _client.AtomicReplaceUnsupported = true;
        _client.FailRename = static request => request.FromPath.EndsWith(".tmp", StringComparison.Ordinal);

        var result = await SaveAsync(Edited, version);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Contain("original file was not changed");
        _client.ReadText(FilePath).Should().Be(Original);
        AuxiliaryFilesExceptBackups().Should().BeEmpty();
    }

    [TestMethod]
    public async Task Save_ManyTimes_KeepsOnlyNewestBackups()
    {
        var version = await OpenVersionAsync();
        for (var i = 0; i < EditorGraph.DefaultMaxBackups + 2; i++)
        {
            var text = $"revision: {i}\n";
            var result = await SaveAsync(text, version);
            version = result.Value.Saved!.Version;
        }

        var backups = await _graph.Catalog.ListAsync(_client, _location, TestContext.CancellationToken);

        backups.Value.Should().HaveCount(EditorGraph.DefaultMaxBackups);
        _client.ReadText(backups.Value[0].Path).Should().Be($"revision: {EditorGraph.DefaultMaxBackups}\n");
    }

    [TestMethod]
    public async Task Save_ContentOverLimit_IsRejectedWithoutTouchingServer()
    {
        var version = await OpenVersionAsync();
        var huge = new string('x', (int)EditorGraph.DefaultMaxFileBytes + 1);

        var result = await SaveAsync(huge, version);

        result.Error!.Code.Should().Be(EditorErrorCode.TooLarge);
        _client.CreateCount.Should().Be(0);
    }

    [TestMethod]
    public async Task Rollback_RestoresBackupContentAndBacksUpCurrent()
    {
        var version = await OpenVersionAsync();
        var saved = await SaveAsync(Edited, version);
        var backupName = saved.Value.Saved!.Backup!.Name;
        var request = new RollbackRequest(_location, backupName, saved.Value.Saved.Version);

        var result = await _graph.Rollback.RollbackAsync(_client, request, TestContext.CancellationToken);

        result.Value.IsSaved.Should().BeTrue();
        _client.ReadText(FilePath).Should().Be(Original);
        _client.ReadText(result.Value.Saved!.Backup!.Path).Should().Be(Edited);
    }

    [TestMethod]
    public async Task Rollback_WithStaleVersion_ReturnsConflict()
    {
        var version = await OpenVersionAsync();
        var saved = await SaveAsync(Edited, version);
        var backupName = saved.Value.Saved!.Backup!.Name;
        var request = new RollbackRequest(_location, backupName, version);

        var result = await _graph.Rollback.RollbackAsync(_client, request, TestContext.CancellationToken);

        result.Value.IsSaved.Should().BeFalse();
        _client.ReadText(FilePath).Should().Be(Edited);
    }

    [TestMethod]
    public async Task Rollback_ForeignBackupName_IsInvalid()
    {
        var version = await OpenVersionAsync();
        var request = new RollbackRequest(_location, "../../etc/shadow", version);

        var result = await _graph.Rollback.RollbackAsync(_client, request, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(EditorErrorCode.Invalid);
    }

    private async Task<RemoteFileVersion> OpenVersionAsync()
    {
        var opened = await _graph.Documents.ReadAsync(_client, FilePath, TestContext.CancellationToken);
        opened.IsSuccess.Should().BeTrue(opened.Error?.Message);
        return opened.Value.Version;
    }

    private Task<EditorResult<RemoteSaveResult>> SaveAsync(string text, RemoteFileVersion? expectedVersion)
    {
        var content = Encoding.UTF8.GetBytes(text);
        var request = new RemoteSaveRequest(_location, content, expectedVersion);
        return _graph.Saver.SaveAsync(_client, request, TestContext.CancellationToken);
    }

    private List<string> AuxiliaryFilesExceptBackups()
    {
        var names = _client.NamesIn(Directory);
        var leftovers = new List<string>();
        for (var i = 0; i < names.Count; i++)
        {
            var name = names[i];
            var isAuxiliary = name.Contains(".termforge-", StringComparison.Ordinal);
            var isBackup = name.EndsWith(".bak", StringComparison.Ordinal);
            if (isAuxiliary && !isBackup)
            {
                leftovers.Add(name);
            }
        }

        return leftovers;
    }
}
