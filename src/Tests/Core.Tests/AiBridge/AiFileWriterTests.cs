using DTech.Core.AiBridge;
using DTech.Core.AiBridge.Approvals;
using DTech.Core.AiBridge.Files;
using DTech.Core.Editor;
using DTech.Core.Tests.AiBridge.Support;
using DTech.Core.Tests.Editor;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.AiBridge;

[TestClass]
public sealed class AiFileWriterTests
{
    private const string FilePath = "/srv/app/.env";
    private const int FileMode = 0b110_100_000;

    private AiBridgeTestGraph _graph = null!;
    private InMemoryEditorFileClient _client = null!;
    private AiFileReader _reader = null!;
    private AiFileWriter _writer = null!;
    private Guid _serverId;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _graph = new AiBridgeTestGraph();
        var server = await _graph.AddServerAsync("prod-web", TestContext.CancellationToken);
        _serverId = server.Id;
        _client = new InMemoryEditorFileClient();
        _client.Seed(FilePath, "A=1\n", FileMode);
        var opener = Substitute.For<IRemoteEditorConnectionOpener>();
        opener.OpenAsync(_serverId, Arg.Any<CancellationToken>())
            .Returns(_ => EditorResult<RemoteEditorSession>.Success(new RemoteEditorSession(_client, "prod-web")));
        var timeProvider = new SteppingTimeProvider(new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));
        var editorGraph = EditorGraph.CreateDefault(timeProvider);
        var editorRecorder = new EditorOperationRecorder(_graph.Docker.Journal);
        var editor = new RemoteFileEditor(opener, editorGraph.Paths, editorGraph.Documents, editorGraph.Catalog);
        var saveService = new RemoteFileSaveService(opener, editorGraph.Paths, editorGraph.Codec, editorGraph.Saver, editorGraph.Rollback, editorGraph.Documents, editorRecorder, _graph.Docker.Safety.Gate);
        _reader = new AiFileReader(_graph.AccessPolicy, editor);
        _writer = new AiFileWriter(_graph.AccessPolicy, _graph.Gate, editor, saveService);
    }

    [TestMethod]
    public async Task Write_WithCurrentHash_ReplacesTheFileAndShowsTheDiff()
    {
        var read = await _reader.ReadAsync(_serverId, FilePath, TestContext.CancellationToken);

        var write = new AiFileWrite(_serverId, FilePath, "A=2\n", read.Value.Version.Sha256);
        var written = await _writer.WriteAsync(write, TestContext.CancellationToken);

        written.Value.Path.Should().Be(FilePath);
        written.Value.BackupName.Should().NotBeNull();
        _client.ReadText(FilePath).Should().Be("A=2\n");
        _client.ModeOf(FilePath).Should().Be(FileMode);
        var change = _graph.Notifier.Requested.Should().ContainSingle().Subject.FileChange;
        change.Should().Be(new AiFileChange(FilePath, "A=1\n", "A=2\n"));
    }

    [TestMethod]
    public async Task Write_WithStaleHash_ReturnsConflictWithoutAsking()
    {
        var write = new AiFileWrite(_serverId, FilePath, "A=2\n", "0000");
        var written = await _writer.WriteAsync(write, TestContext.CancellationToken);

        written.Error!.Code.Should().Be(AiBridgeErrorCode.Conflict);
        _graph.Notifier.Requested.Should().BeEmpty();
        _client.ReadText(FilePath).Should().Be("A=1\n");
    }

    [TestMethod]
    public async Task Write_ExistingFileWithoutHash_ReturnsConflict()
    {
        var write = new AiFileWrite(_serverId, FilePath, "A=2\n", null);
        var written = await _writer.WriteAsync(write, TestContext.CancellationToken);

        written.Error!.Code.Should().Be(AiBridgeErrorCode.Conflict);
        _client.ReadText(FilePath).Should().Be("A=1\n");
    }

    [TestMethod]
    public async Task Write_NewFileWithoutHash_CreatesIt()
    {
        var write = new AiFileWrite(_serverId, "/srv/app/new.conf", "x=1\n", null);
        var written = await _writer.WriteAsync(write, TestContext.CancellationToken);

        written.IsSuccess.Should().BeTrue();
        _client.ReadText("/srv/app/new.conf").Should().Be("x=1\n");
        _graph.Notifier.Requested.Should().ContainSingle().Which.Summary.Should().Be("Create /srv/app/new.conf");
    }

    [TestMethod]
    public async Task Write_Denied_LeavesTheFileUnchanged()
    {
        _graph.Notifier.Answer = AiApprovalOutcome.Denied;
        var read = await _reader.ReadAsync(_serverId, FilePath, TestContext.CancellationToken);

        var write = new AiFileWrite(_serverId, FilePath, "A=2\n", read.Value.Version.Sha256);
        var written = await _writer.WriteAsync(write, TestContext.CancellationToken);

        written.Error!.Code.Should().Be(AiBridgeErrorCode.Denied);
        _client.ReadText(FilePath).Should().Be("A=1\n");
    }
}
