using System.Runtime.Versioning;
using DTech.Core.Files;
using DTech.Core.Files.Local;
using DTech.Core.Files.Search;
using DTech.Core.Tests.SavedConnections.Support;
using FluentAssertions;

namespace DTech.Core.Tests.Files.Search;

[TestClass]
public sealed class LocalFileSearchServiceTests
{
    private TemporaryDirectory _directory = null!;
    private LocalFileSearchService _service = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = new TemporaryDirectory();
        var pathResolver = new LocalPathResolver();
        var entryFactory = new LocalEntryFactory();
        var reader = new LocalFileTreeReader(entryFactory);
        var searcher = new FileTreeSearcher(TimeProvider.System);
        _service = new LocalFileSearchService(pathResolver, reader, searcher);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _directory.Dispose();
    }

    [TestMethod]
    public async Task Start_FindsMatchingFilesInNestedDirectories()
    {
        _directory.Write("app.log", "a");
        _directory.Write(Path.Combine("nested", "deeper", "error.LOG"), "b");
        _directory.Write(Path.Combine("nested", "notes.txt"), "c");
        var request = new FileSearchRequest(_directory.Path, "*.log");

        var events = await RunAsync(request);

        var matchedNames = events.OfType<FileSearchMatch>().Select(match => match.Entry.Name);
        matchedNames.Should().BeEquivalentTo("app.log", "error.LOG");
        var summary = Summary(events);
        summary.Outcome.Should().Be(FileSearchOutcome.Completed);
        summary.Method.Should().Be(FileSearchMethod.Local);
        summary.IsPartial.Should().BeFalse();
    }

    [TestMethod]
    public async Task Start_DepthLimit_SkipsDeeperMatchesAndMarksPartial()
    {
        _directory.Write("top.log", "a");
        _directory.Write(Path.Combine("nested", "deep.log"), "b");
        var request = new FileSearchRequest(_directory.Path, "*.log") { MaxDepth = 1 };

        var events = await RunAsync(request);

        events.OfType<FileSearchMatch>().Select(match => match.Entry.Name).Should().Equal("top.log");
        var summary = Summary(events);
        summary.DepthLimitReached.Should().BeTrue();
        summary.IsPartial.Should().BeTrue();
    }

    [TestMethod]
    public async Task Start_ResultLimit_StopsAndMarksPartial()
    {
        for (var i = 0; i < 5; i++)
        {
            _directory.Write($"file{i}.log", "x");
        }

        var request = new FileSearchRequest(_directory.Path, "*.log") { MaxResults = 2 };

        var events = await RunAsync(request);

        events.OfType<FileSearchMatch>().Should().HaveCount(2);
        var summary = Summary(events);
        summary.Outcome.Should().Be(FileSearchOutcome.ResultLimitReached);
        summary.IsPartial.Should().BeTrue();
    }

    [TestMethod]
    public async Task Start_CancelledByCaller_ReportsCancelled()
    {
        _directory.Write("a.log", "x");
        var request = new FileSearchRequest(_directory.Path, "*.log");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var events = await RunAsync(request, cancellation.Token);

        Summary(events).Outcome.Should().Be(FileSearchOutcome.Cancelled);
    }

    [TestMethod]
    [UnsupportedOSPlatform("windows")]
    public async Task Start_UnreadableSubdirectory_IsReportedAndSearchContinues()
    {
        if (OperatingSystem.IsWindows() || Environment.IsPrivilegedProcess)
        {
            Assert.Inconclusive("Needs a non-privileged POSIX user.");
        }

        _directory.Write("visible.log", "a");
        var locked = _directory.Combine("locked");
        Directory.CreateDirectory(locked);
        File.SetUnixFileMode(locked, UnixFileMode.None);
        try
        {
            var request = new FileSearchRequest(_directory.Path, "*.log");

            var events = await RunAsync(request);

            events.OfType<FileSearchMatch>().Select(match => match.Entry.Name).Should().Equal("visible.log");
            var warning = events.OfType<FileSearchWarning>().Single();
            warning.Path.Should().Be(locked);
            warning.Error.Code.Should().Be(FileErrorCode.PermissionDenied);
            var summary = Summary(events);
            summary.Outcome.Should().Be(FileSearchOutcome.Completed);
            summary.IsPartial.Should().BeTrue();
        }
        finally
        {
            File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [TestMethod]
    public void Start_MissingRoot_IsNotFound()
    {
        var missing = _directory.Combine("missing");
        var request = new FileSearchRequest(missing, "x");

        var started = _service.Start(request);

        started.Error!.Code.Should().Be(FileErrorCode.NotFound);
    }

    [TestMethod]
    public void Start_FileAsRoot_IsNotADirectory()
    {
        var file = _directory.Write("plain.txt", "x");
        var request = new FileSearchRequest(file, "x");

        var started = _service.Start(request);

        started.Error!.Code.Should().Be(FileErrorCode.NotADirectory);
    }

    [TestMethod]
    public void Start_PatternWithPathSeparator_IsInvalid()
    {
        var request = new FileSearchRequest(_directory.Path, "../etc/passwd");

        var started = _service.Start(request);

        started.Error!.Code.Should().Be(FileErrorCode.Invalid);
    }

    private Task<List<FileSearchEvent>> RunAsync(FileSearchRequest request)
    {
        return RunAsync(request, TestContext.CancellationToken);
    }

    private async Task<List<FileSearchEvent>> RunAsync(FileSearchRequest request, CancellationToken cancellationToken)
    {
        var started = _service.Start(request);
        started.IsSuccess.Should().BeTrue(started.Error?.Message);
        await using var execution = started.Value;
        var events = execution.ExecuteAsync(cancellationToken);
        return await events.ToListAsync(CancellationToken.None);
    }

    private static FileSearchSummary Summary(List<FileSearchEvent> events)
    {
        events.Last().Should().BeOfType<FileSearchCompleted>();
        return ((FileSearchCompleted)events.Last()).Summary;
    }
}
