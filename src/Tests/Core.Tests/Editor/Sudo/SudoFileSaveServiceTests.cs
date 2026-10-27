using DTech.Core.Editor;
using DTech.Core.Editor.Sudo;
using DTech.Core.Operations;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Editor.Sudo;

[TestClass]
public sealed class SudoFileSaveServiceTests
{
    private const string FilePath = "/etc/app/app.conf";
    private const string ServerName = "web-01";
    private const string Password = "pw";
    private const int FileMode = 0b110_100_000;

    private static readonly Guid ConnectionId = Guid.NewGuid();
    private static readonly Guid OperationId = Guid.NewGuid();
    private static readonly DateTimeOffset Start = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private InMemoryEditorFileClient _client = null!;
    private ISudoEditorSessionOpener _opener = null!;
    private IOperationJournal _journal = null!;
    private SudoFileSaveService _service = null!;
    private SudoFileEditor _editor = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _client = new InMemoryEditorFileClient();
        _client.Seed(FilePath, "A=1\n", FileMode);
        _opener = Substitute.For<ISudoEditorSessionOpener>();
        _opener.OpenAsync(ConnectionId, Arg.Any<SudoCredentials>(), Arg.Any<CancellationToken>()).Returns(_ => OpenSession());
        _journal = Substitute.For<IOperationJournal>();
        var now = Start.UtcDateTime;
        var operation = new Operation { Id = OperationId, Kind = EditorOperationRecorder.SaveKind, Status = OperationStatus.Running, CreatedAt = now, UpdatedAt = now };
        _journal.Create(Arg.Any<CreateOperationRequest>(), Arg.Any<CancellationToken>()).Returns(operation);
        var timeProvider = new SteppingTimeProvider(Start);
        var graph = EditorGraph.CreateDefault(timeProvider);
        var recorder = new EditorOperationRecorder(_journal);
        _service = new SudoFileSaveService(_opener, graph.Paths, graph.Codec, graph.Saver, graph.Rollback, graph.Documents, recorder);
        _editor = new SudoFileEditor(_opener, graph.Paths, graph.Documents, graph.Catalog);
    }

    [TestMethod]
    public async Task Save_IsJournaledAsElevatedWithoutPassword()
    {
        var credentials = new SudoCredentials(Password, false);
        var openRequest = new SudoDocumentRequest(ConnectionId, FilePath, credentials);
        var opened = await _editor.OpenAsync(openRequest, TestContext.CancellationToken);
        var content = new TextContent("A=2\n", false);
        var document = new SaveDocumentRequest(FilePath, content, opened.Value.Version);
        var request = new SudoSaveRequest(ConnectionId, document, credentials);

        var result = await _service.SaveAsync(request, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        _client.ReadText(FilePath).Should().Be("A=2\n");
        await _journal.Received(1).Create(Arg.Is<CreateOperationRequest>(created => created.Kind == EditorOperationRecorder.SaveKind && created.Details!["elevation"] == "sudo" && !created.Details.Values.Contains(Password)), Arg.Any<CancellationToken>());
        await _opener.Received(2).OpenAsync(ConnectionId, credentials, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Save_PassesOriginalOwnerToBackupAndTemporaryFile()
    {
        var credentials = new SudoCredentials(Password, false);
        var openRequest = new SudoDocumentRequest(ConnectionId, FilePath, credentials);
        var opened = await _editor.OpenAsync(openRequest, TestContext.CancellationToken);
        var content = new TextContent("A=2\n", false);
        var document = new SaveDocumentRequest(FilePath, content, opened.Value.Version);
        var request = new SudoSaveRequest(ConnectionId, document, credentials);

        await _service.SaveAsync(request, TestContext.CancellationToken);

        var expectedOwner = new RemoteFileOwner(InMemoryEditorFileClient.OwnerId, InMemoryEditorFileClient.OwnerId);
        _client.Creations.Should().HaveCount(2);
        _client.Creations.Should().OnlyContain(creation => creation.Owner == expectedOwner && creation.Mode == FileMode);
    }

    [TestMethod]
    public async Task Save_SessionRefused_ReturnsSudoErrorWithoutJournaling()
    {
        var refused = new EditorError(EditorErrorCode.SudoPasswordIncorrect, SudoErrors.PasswordIncorrectMessage);
        var failure = EditorResult<RemoteEditorSession>.Failure(refused);
        _opener.OpenAsync(ConnectionId, Arg.Any<SudoCredentials>(), Arg.Any<CancellationToken>()).Returns(failure);
        var content = new TextContent("A=2\n", false);
        var document = new SaveDocumentRequest(FilePath, content, null);
        var request = new SudoSaveRequest(ConnectionId, document, SudoCredentials.None);

        var result = await _service.SaveAsync(request, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(EditorErrorCode.SudoPasswordIncorrect);
        await _journal.DidNotReceive().Create(Arg.Any<CreateOperationRequest>(), Arg.Any<CancellationToken>());
        _client.ReadText(FilePath).Should().Be("A=1\n");
    }

    [TestMethod]
    public async Task Rollback_IsJournaledAsElevatedAndReturnsDocument()
    {
        var credentials = new SudoCredentials(Password, false);
        var openRequest = new SudoDocumentRequest(ConnectionId, FilePath, credentials);
        var opened = await _editor.OpenAsync(openRequest, TestContext.CancellationToken);
        var content = new TextContent("A=2\n", false);
        var document = new SaveDocumentRequest(FilePath, content, opened.Value.Version);
        var saveRequest = new SudoSaveRequest(ConnectionId, document, credentials);
        var saved = await _service.SaveAsync(saveRequest, TestContext.CancellationToken);
        var savedFile = saved.Value.Result.Saved!;
        var rollbackDocument = new RollbackDocumentRequest(FilePath, savedFile.Backup!.Name, savedFile.Version);
        var request = new SudoRollbackRequest(ConnectionId, rollbackDocument, credentials);

        var result = await _service.RollbackAsync(request, TestContext.CancellationToken);

        result.Value.Document!.Content.Text.Should().Be("A=1\n");
        await _journal.Received(1).Create(Arg.Is<CreateOperationRequest>(created => created.Kind == EditorOperationRecorder.RollbackKind && created.Details!["elevation"] == "sudo"), Arg.Any<CancellationToken>());
    }

    private EditorResult<RemoteEditorSession> OpenSession()
    {
        var session = new RemoteEditorSession(_client, ServerName);
        return EditorResult<RemoteEditorSession>.Success(session);
    }
}
