using DTech.Core.Connections;
using DTech.Core.Diagnostics;
using DTech.Core.Files;
using DTech.Core.Files.Remote;
using DTech.Core.Files.Search;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Files.Search;

[TestClass]
public sealed class RemoteFileSearchServiceTests
{
    private const string ToolLookupCommand = "command -v find";

    private static readonly Guid ConnectionId = Guid.NewGuid();

    private IRemoteConnectionOpener _connectionOpener = null!;
    private IRemoteFileClient _fileClient = null!;
    private IRemoteCommandRunnerOpener _runnerOpener = null!;
    private IRemoteCommandRunner _runner = null!;
    private CapabilityReportCache _reportCache = null!;
    private RemoteFileSearchService _service = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _fileClient = Substitute.For<IRemoteFileClient>();
        _connectionOpener = Substitute.For<IRemoteConnectionOpener>();
        var openedClient = FileResult<IRemoteFileClient>.Success(_fileClient);
        _connectionOpener.OpenAsync(ConnectionId, Arg.Any<CancellationToken>()).Returns(openedClient);
        _runner = Substitute.For<IRemoteCommandRunner>();
        _runnerOpener = Substitute.For<IRemoteCommandRunnerOpener>();
        var openedRunner = FileResult<IRemoteCommandRunner>.Success(_runner);
        _runnerOpener.OpenAsync(ConnectionId, Arg.Any<CancellationToken>()).Returns(openedRunner);
        _reportCache = new CapabilityReportCache();
        var availability = new RemoteFindAvailability(_reportCache);
        var launcher = new RemoteFindLauncher(_runnerOpener, availability);
        var normalizer = new RemotePathNormalizer();
        var searcher = new FileTreeSearcher(TimeProvider.System);
        _service = new RemoteFileSearchService(_connectionOpener, normalizer, launcher, searcher);
        StubListing("/srv", Entry("app.log", "/srv/app.log", RemoteFileKind.File));
    }

    [TestMethod]
    public async Task Start_DefaultMethod_TraversesOverSftpWithoutRunningCommands()
    {
        var request = CreateRequest("*.log", false);

        var events = await RunAsync(request);

        events.OfType<FileSearchMatch>().Select(match => match.Entry.Path).Should().Equal("/srv/app.log");
        Summary(events).Method.Should().Be(FileSearchMethod.Sftp);
        await _runnerOpener.DidNotReceive().OpenAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Start_RemoteFindAvailable_RunsFindWithQuotedPattern()
    {
        StubToolLookup(0);
        var findOutput = new RemoteCommandResult(0, "f/srv/app.log\0d/srv/logs\0", string.Empty);
        var foundResult = SshResult<RemoteCommandResult>.Success(findOutput);
        _runner.RunAsync(Arg.Is<RemoteCommandRequest>(request => request.Command.Contains("find ", StringComparison.Ordinal) && !request.Command.Contains(ToolLookupCommand, StringComparison.Ordinal)), Arg.Any<CancellationToken>()).Returns(foundResult);
        var request = CreateRequest("x'; rm -rf ~; '", true);

        var events = await RunAsync(request);

        events.OfType<FileSearchMatch>().Should().HaveCount(2);
        Summary(events).Method.Should().Be(FileSearchMethod.RemoteFind);
        await _connectionOpener.DidNotReceive().OpenAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _runner.Received(1).RunAsync(Arg.Is<RemoteCommandRequest>(sent => sent.Command.Contains("-iname '\\''*x'\\''\\'\\'''\\''; rm -rf ~; '\\''\\'\\'''\\''*'\\''", StringComparison.Ordinal)), Arg.Any<CancellationToken>());
        await _runner.Received(1).DisposeAsync();
    }

    [TestMethod]
    public async Task Start_FindMissing_FallsBackToSftpWithNotice()
    {
        StubToolLookup(1);
        var request = CreateRequest("*.log", true);

        var events = await RunAsync(request);

        var notice = events.OfType<FileSearchNotice>().Single();
        notice.Message.Should().Contain("'find' is not installed").And.Contain("SFTP");
        events.OfType<FileSearchMatch>().Should().ContainSingle();
        Summary(events).Method.Should().Be(FileSearchMethod.Sftp);
        await _runner.Received(1).DisposeAsync();
    }

    [TestMethod]
    public async Task Start_CommandExecutionUnavailable_FallsBackWithoutProbing()
    {
        var execution = new CapabilityResult(CapabilityIds.CommandExecution, "Command execution", CapabilityStatus.Unavailable, "exec channel rejected");
        var report = new CapabilityReport(ConnectionId, DateTime.UtcNow, CapabilityReportState.Completed, [execution]);
        _reportCache.Store(report);
        var request = CreateRequest("*.log", true);

        var events = await RunAsync(request);

        events.OfType<FileSearchNotice>().Single().Message.Should().Contain("exec channel rejected");
        Summary(events).Method.Should().Be(FileSearchMethod.Sftp);
        await _runner.DidNotReceive().RunAsync(Arg.Any<RemoteCommandRequest>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Start_RunnerConnectionFails_FallsBackToSftpWithNotice()
    {
        var connectionError = new FileError(FileErrorCode.ConnectionFailed, "exec refused");
        var failed = FileResult<IRemoteCommandRunner>.Failure(connectionError);
        _runnerOpener.OpenAsync(ConnectionId, Arg.Any<CancellationToken>()).Returns(failed);
        var request = CreateRequest("*.log", true);

        var events = await RunAsync(request);

        events.OfType<FileSearchNotice>().Single().Message.Should().Contain("exec refused");
        Summary(events).Method.Should().Be(FileSearchMethod.Sftp);
    }

    [TestMethod]
    public async Task Start_TraversalRoot_IsRejectedWithoutConnecting()
    {
        var search = new FileSearchRequest("/srv/../../etc", "x");
        var request = new RemoteFileSearchRequest(search, true);

        var started = await _service.StartAsync(ConnectionId, request, TestContext.CancellationToken);

        started.Error!.Code.Should().Be(FileErrorCode.Invalid);
        await _connectionOpener.DidNotReceive().OpenAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _runnerOpener.DidNotReceive().OpenAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Start_PermissionDeniedSubtreeOverSftp_IsReportedAsWarning()
    {
        var locked = Entry("locked", "/srv/locked", RemoteFileKind.Directory);
        var visible = Entry("app.log", "/srv/app.log", RemoteFileKind.File);
        StubListing("/srv", locked, visible);
        var denied = new SshError(SshErrorKind.PermissionDenied, "Permission denied");
        var deniedResult = SshResult<IReadOnlyList<RemoteFileEntry>>.Failure(denied);
        _fileClient.ListAsync("/srv/locked", Arg.Any<CancellationToken>()).Returns(deniedResult);
        var request = CreateRequest("*", false);

        var events = await RunAsync(request);

        var warning = events.OfType<FileSearchWarning>().Single();
        warning.Path.Should().Be("/srv/locked");
        warning.Error.Code.Should().Be(FileErrorCode.PermissionDenied);
        var summary = Summary(events);
        summary.Outcome.Should().Be(FileSearchOutcome.Completed);
        summary.IsPartial.Should().BeTrue();
    }

    private static RemoteFileSearchRequest CreateRequest(string pattern, bool useRemoteFind)
    {
        var search = new FileSearchRequest("/srv", pattern);
        return new RemoteFileSearchRequest(search, useRemoteFind);
    }

    private static RemoteFileEntry Entry(string name, string path, RemoteFileKind kind)
    {
        return new RemoteFileEntry(name, path, kind);
    }

    private void StubListing(string path, params RemoteFileEntry[] entries)
    {
        IReadOnlyList<RemoteFileEntry> listed = entries;
        var result = SshResult<IReadOnlyList<RemoteFileEntry>>.Success(listed);
        _fileClient.ListAsync(path, Arg.Any<CancellationToken>()).Returns(result);
    }

    private void StubToolLookup(int exitCode)
    {
        var lookup = new RemoteCommandResult(exitCode, string.Empty, string.Empty);
        var lookupResult = SshResult<RemoteCommandResult>.Success(lookup);
        _runner.RunAsync(Arg.Is<RemoteCommandRequest>(request => request.Command.Contains(ToolLookupCommand, StringComparison.Ordinal)), Arg.Any<CancellationToken>()).Returns(lookupResult);
    }

    private async Task<List<FileSearchEvent>> RunAsync(RemoteFileSearchRequest request)
    {
        var started = await _service.StartAsync(ConnectionId, request, TestContext.CancellationToken);
        started.IsSuccess.Should().BeTrue(started.Error?.Message);
        await using var execution = started.Value;
        var events = execution.ExecuteAsync(TestContext.CancellationToken);
        return await events.ToListAsync(CancellationToken.None);
    }

    private static FileSearchSummary Summary(List<FileSearchEvent> events)
    {
        events.Last().Should().BeOfType<FileSearchCompleted>();
        return ((FileSearchCompleted)events.Last()).Summary;
    }
}
