using DTech.Core.Editor;
using DTech.Core.Operations;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Editor;

[TestClass]
public sealed class RemoteFileSaveServiceTests
{
    private const string FilePath = "/srv/app/.env";
    private const string ServerName = "prod-web";
    private const int FileMode = 0b110_100_000;

    private static readonly Guid ConnectionId = Guid.NewGuid();
    private static readonly Guid OperationId = Guid.NewGuid();
    private static readonly DateTimeOffset Start = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private InMemoryEditorFileClient _client = null!;
    private IRemoteEditorConnectionOpener _opener = null!;
    private IOperationJournal _journal = null!;
    private EditorGraph _graph = null!;
    private RemoteFileSaveService _service = null!;
    private RemoteFileEditor _editor = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _client = new InMemoryEditorFileClient();
        _client.Seed(FilePath, "A=1\n", FileMode);
        _opener = Substitute.For<IRemoteEditorConnectionOpener>();
        _opener.OpenAsync(ConnectionId, Arg.Any<CancellationToken>()).Returns(_ => OpenSession());
        _journal = Substitute.For<IOperationJournal>();
        var now = Start.UtcDateTime;
        var operation = new Operation { Id = OperationId, Kind = EditorOperationRecorder.SaveKind, Status = OperationStatus.Running, CreatedAt = now, UpdatedAt = now };
        _journal.Create(Arg.Any<CreateOperationRequest>(), Arg.Any<CancellationToken>()).Returns(operation);
        var timeProvider = new SteppingTimeProvider(Start);
        _graph = EditorGraph.CreateDefault(timeProvider);
        var recorder = new EditorOperationRecorder(_journal);
        _service = new RemoteFileSaveService(_opener, _graph.Paths, _graph.Codec, _graph.Saver, _graph.Rollback, _graph.Documents, recorder);
        _editor = new RemoteFileEditor(_opener, _graph.Paths, _graph.Documents, _graph.Catalog);
    }

    [TestMethod]
    public async Task Open_ReturnsDecodedDocumentWithVersionAndMode()
    {
        var opened = await _editor.OpenAsync(ConnectionId, FilePath, TestContext.CancellationToken);

        opened.Value.Content.Text.Should().Be("A=1\n");
        opened.Value.Mode.Should().Be(FileMode);
        opened.Value.Version.Size.Should().Be(4);
    }

    [TestMethod]
    public async Task Open_BinaryFile_IsRejected()
    {
        byte[] binary = [0x00, 0x01, 0x02];
        _client.SeedBytes(FilePath, binary, FileMode);

        var opened = await _editor.OpenAsync(ConnectionId, FilePath, TestContext.CancellationToken);

        opened.Error!.Code.Should().Be(EditorErrorCode.Binary);
    }

    [TestMethod]
    public async Task Open_FileOverLimit_IsTooLarge()
    {
        var huge = new string('x', (int)EditorGraph.DefaultMaxFileBytes + 1);
        _client.Seed(FilePath, huge, FileMode);

        var opened = await _editor.OpenAsync(ConnectionId, FilePath, TestContext.CancellationToken);

        opened.Error!.Code.Should().Be(EditorErrorCode.TooLarge);
    }

    [TestMethod]
    public async Task Save_Success_IsJournaledWithBackupAndHash()
    {
        var opened = await _editor.OpenAsync(ConnectionId, FilePath, TestContext.CancellationToken);
        var content = new TextContent("A=2\n", false);
        var request = new SaveDocumentRequest(FilePath, content, opened.Value.Version);

        var result = await _service.SaveAsync(ConnectionId, request, TestContext.CancellationToken);

        result.Value.OperationId.Should().Be(OperationId);
        await _journal.Received(1).Create(Arg.Is<CreateOperationRequest>(created => created.Kind == EditorOperationRecorder.SaveKind && created.Server == ServerName && created.Details!["path"] == FilePath), Arg.Any<CancellationToken>());
        await _journal.Received(1).Start(OperationId, Arg.Any<CancellationToken>());
        await _journal.Received(1).Succeed(OperationId, Arg.Is<IReadOnlyDictionary<string, string>>(details => details.ContainsKey("backup") && details["sha256"].Length == 64), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Save_Conflict_IsJournaledAsFailure()
    {
        var opened = await _editor.OpenAsync(ConnectionId, FilePath, TestContext.CancellationToken);
        _client.Seed(FilePath, "A=3\n", FileMode);
        var content = new TextContent("A=2\n", false);
        var request = new SaveDocumentRequest(FilePath, content, opened.Value.Version);

        var result = await _service.SaveAsync(ConnectionId, request, TestContext.CancellationToken);

        result.Value.Result.IsSaved.Should().BeFalse();
        await _journal.Received(1).Fail(OperationId, Arg.Is<string>(error => error.StartsWith("Not saved", StringComparison.Ordinal)), Arg.Any<CancellationToken>());
        _client.ReadText(FilePath).Should().Be("A=3\n");
    }

    [TestMethod]
    public async Task Save_WriteFailure_IsJournaledAsFailure()
    {
        var opened = await _editor.OpenAsync(ConnectionId, FilePath, TestContext.CancellationToken);
        _client.FailAtomicReplace = true;
        var content = new TextContent("A=2\n", false);
        var request = new SaveDocumentRequest(FilePath, content, opened.Value.Version);

        var result = await _service.SaveAsync(ConnectionId, request, TestContext.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        await _journal.Received(1).Fail(OperationId, Arg.Any<string>(), Arg.Any<CancellationToken>());
        _client.ReadText(FilePath).Should().Be("A=1\n");
    }

    [TestMethod]
    public async Task Save_InvalidPath_NeverConnects()
    {
        var content = new TextContent("x", false);
        var request = new SaveDocumentRequest("/srv/../etc/passwd", content, null);

        var result = await _service.SaveAsync(ConnectionId, request, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(EditorErrorCode.Invalid);
        await _opener.DidNotReceive().OpenAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Rollback_ReturnsRestoredDocumentAndListsBackups()
    {
        var opened = await _editor.OpenAsync(ConnectionId, FilePath, TestContext.CancellationToken);
        var content = new TextContent("A=2\n", false);
        var saveRequest = new SaveDocumentRequest(FilePath, content, opened.Value.Version);
        var saved = await _service.SaveAsync(ConnectionId, saveRequest, TestContext.CancellationToken);
        var savedFile = saved.Value.Result.Saved!;
        var rollbackRequest = new RollbackDocumentRequest(FilePath, savedFile.Backup!.Name, savedFile.Version);

        var rolledBack = await _service.RollbackAsync(ConnectionId, rollbackRequest, TestContext.CancellationToken);
        var backups = await _editor.ListBackupsAsync(ConnectionId, FilePath, TestContext.CancellationToken);

        rolledBack.Value.Document!.Content.Text.Should().Be("A=1\n");
        backups.Value.Should().HaveCount(2);
        await _journal.Received(1).Create(Arg.Is<CreateOperationRequest>(created => created.Kind == EditorOperationRecorder.RollbackKind && created.Details!["restoredFrom"] == savedFile.Backup.Name), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ReadBackup_ReturnsBackupContent()
    {
        var opened = await _editor.OpenAsync(ConnectionId, FilePath, TestContext.CancellationToken);
        var content = new TextContent("A=2\n", false);
        var saveRequest = new SaveDocumentRequest(FilePath, content, opened.Value.Version);
        var saved = await _service.SaveAsync(ConnectionId, saveRequest, TestContext.CancellationToken);
        var reference = new BackupReference(FilePath, saved.Value.Result.Saved!.Backup!.Name);

        var backup = await _editor.ReadBackupAsync(ConnectionId, reference, TestContext.CancellationToken);

        backup.Value.Content.Text.Should().Be("A=1\n");
    }

    private EditorResult<RemoteEditorSession> OpenSession()
    {
        var session = new RemoteEditorSession(_client, ServerName);
        return EditorResult<RemoteEditorSession>.Success(session);
    }
}
