using DTech.Core.Files;
using DTech.Core.Files.Search;
using DTech.Core.Tests.Reconnect.Support;
using FluentAssertions;

namespace DTech.Core.Tests.Files.Search;

[TestClass]
public sealed class FileTreeSearcherTests
{
    private static readonly TimeSpan GenerousTimeLimit = TimeSpan.FromMinutes(5);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Search_FindsMatchesAtEveryLevelWithParentPath()
    {
        var tree = new InMemoryFileTree()
            .AddFile("/srv/app.log")
            .AddFile("/srv/data/archive/old.LOG")
            .AddFile("/srv/data/readme.txt");
        var query = CreateQuery("log", 8, 100);

        var events = await SearchAsync(tree, query);

        var matches = events.OfType<FileSearchMatch>().ToList();
        matches.Select(match => match.Entry.Path).Should().Equal("/srv/app.log", "/srv/data/archive/old.LOG");
        matches[1].ParentPath.Should().Be("/srv/data/archive");
        var summary = Summary(events);
        summary.Outcome.Should().Be(FileSearchOutcome.Completed);
        summary.MatchCount.Should().Be(2);
        summary.IsPartial.Should().BeFalse();
    }

    [TestMethod]
    public async Task Search_DepthLimit_DoesNotDescendBelowMaxDepthAndReportsIt()
    {
        var tree = new InMemoryFileTree()
            .AddFile("/srv/one/match-1")
            .AddFile("/srv/one/two/match-2")
            .AddFile("/srv/one/two/three/match-3");
        var query = CreateQuery("match", 2, 100);

        var events = await SearchAsync(tree, query);

        events.OfType<FileSearchMatch>().Select(match => match.Entry.Name).Should().Equal("match-1");
        tree.ListedPaths.Should().NotContain("/srv/one/two");
        var summary = Summary(events);
        summary.Outcome.Should().Be(FileSearchOutcome.Completed);
        summary.DepthLimitReached.Should().BeTrue();
        summary.IsPartial.Should().BeTrue();
    }

    [TestMethod]
    public async Task Search_DepthOne_MatchesOnlyDirectChildren()
    {
        var tree = new InMemoryFileTree()
            .AddFile("/srv/match-top")
            .AddFile("/srv/sub/match-nested");
        var query = CreateQuery("match", 1, 100);

        var events = await SearchAsync(tree, query);

        events.OfType<FileSearchMatch>().Select(match => match.Entry.Name).Should().Equal("match-top");
        tree.ListedPaths.Should().Equal("/srv");
    }

    [TestMethod]
    public async Task Search_ResultLimit_StopsAndReportsPartial()
    {
        var tree = new InMemoryFileTree();
        for (var i = 0; i < 10; i++)
        {
            tree.AddFile($"/srv/file-{i:D2}.txt");
        }

        var query = CreateQuery("file", 8, 3);

        var events = await SearchAsync(tree, query);

        events.OfType<FileSearchMatch>().Should().HaveCount(3);
        var summary = Summary(events);
        summary.Outcome.Should().Be(FileSearchOutcome.ResultLimitReached);
        summary.MatchCount.Should().Be(3);
        summary.IsPartial.Should().BeTrue();
    }

    [TestMethod]
    public async Task Search_ExactlyResultLimitMatches_IsComplete()
    {
        var tree = new InMemoryFileTree()
            .AddFile("/srv/a.txt")
            .AddFile("/srv/b.txt");
        var query = CreateQuery("*.txt", 8, 2);

        var events = await SearchAsync(tree, query);

        Summary(events).Outcome.Should().Be(FileSearchOutcome.Completed);
    }

    [TestMethod]
    public async Task Search_TimeLimit_StopsWhenClockPassesLimit()
    {
        var clock = new ManualTimeProvider();
        var tree = new InMemoryFileTree();
        for (var i = 0; i < 10; i++)
        {
            tree.AddFile($"/srv/dir-{i}/match-{i}");
        }

        tree.BeforeList = (_, _) =>
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            return Task.CompletedTask;
        };
        var query = CreateQuery("match", 8, 100, TimeSpan.FromSeconds(3.5));
        var searcher = new FileTreeSearcher(clock);

        var events = await searcher.SearchAsync(tree, query, TestContext.CancellationToken).ToListAsync(CancellationToken.None);

        var summary = Summary(events);
        summary.Outcome.Should().Be(FileSearchOutcome.TimeLimitReached);
        summary.IsPartial.Should().BeTrue();
        tree.ListedPaths.Should().HaveCountLessThan(6);
        events.OfType<FileSearchMatch>().Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task Search_TimeLimit_InterruptsHangingListing()
    {
        var tree = new InMemoryFileTree().AddFile("/srv/slow/file");
        tree.BeforeList = (path, token) => path == "/srv/slow" ? Task.Delay(Timeout.Infinite, token) : Task.CompletedTask;
        var query = CreateQuery("file", 8, 100, TimeSpan.FromMilliseconds(200));
        var searcher = new FileTreeSearcher(TimeProvider.System);

        var events = await searcher.SearchAsync(tree, query, TestContext.CancellationToken).ToListAsync(CancellationToken.None);

        Summary(events).Outcome.Should().Be(FileSearchOutcome.TimeLimitReached);
    }

    [TestMethod]
    public async Task Search_CallerCancellation_StopsWithCancelledOutcome()
    {
        using var cancellation = new CancellationTokenSource();
        var tree = new InMemoryFileTree()
            .AddFile("/srv/a/match-a")
            .AddFile("/srv/b/match-b");
        tree.BeforeList = (path, _) =>
        {
            if (path == "/srv/a")
            {
                cancellation.Cancel();
            }

            return Task.CompletedTask;
        };
        var query = CreateQuery("match", 8, 100);
        var searcher = new FileTreeSearcher(TimeProvider.System);

        var events = await searcher.SearchAsync(tree, query, cancellation.Token).ToListAsync(CancellationToken.None);

        Summary(events).Outcome.Should().Be(FileSearchOutcome.Cancelled);
        tree.ListedPaths.Should().NotContain("/srv/b");
    }

    [TestMethod]
    public async Task Search_CallerCancellation_InterruptsHangingListing()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var tree = new InMemoryFileTree().AddFile("/srv/slow/file");
        tree.BeforeList = (path, token) => path == "/srv/slow" ? Task.Delay(Timeout.Infinite, token) : Task.CompletedTask;
        var query = CreateQuery("file", 8, 100);
        var searcher = new FileTreeSearcher(TimeProvider.System);

        var events = await searcher.SearchAsync(tree, query, cancellation.Token).ToListAsync(CancellationToken.None);

        Summary(events).Outcome.Should().Be(FileSearchOutcome.Cancelled);
    }

    [TestMethod]
    public async Task Search_PermissionDeniedSubtree_IsReportedAndSearchContinues()
    {
        var tree = new InMemoryFileTree()
            .Deny("/srv/locked")
            .AddFile("/srv/open/match-1")
            .AddFile("/srv/zeta/match-2");
        var query = CreateQuery("match", 8, 100);

        var events = await SearchAsync(tree, query);

        var warning = events.OfType<FileSearchWarning>().Should().ContainSingle().Subject;
        warning.Path.Should().Be("/srv/locked");
        warning.Error.Code.Should().Be(FileErrorCode.PermissionDenied);
        events.OfType<FileSearchMatch>().Should().HaveCount(2);
        var summary = Summary(events);
        summary.Outcome.Should().Be(FileSearchOutcome.Completed);
        summary.WarningCount.Should().Be(1);
        summary.IsPartial.Should().BeTrue();
    }

    [TestMethod]
    public async Task Search_ManyFailingSubtrees_CapsReportedWarnings()
    {
        var tree = new InMemoryFileTree();
        var total = FileSearchLimits.MaxReportedWarnings + 5;
        for (var i = 0; i < total; i++)
        {
            tree.Deny($"/srv/locked-{i:D3}");
        }

        var query = CreateQuery("x", 8, 100);

        var events = await SearchAsync(tree, query);

        events.OfType<FileSearchWarning>().Should().HaveCount(FileSearchLimits.MaxReportedWarnings);
        Summary(events).WarningCount.Should().Be(total);
    }

    [TestMethod]
    public async Task Search_UnreadableRoot_FailsWithMessage()
    {
        var tree = new InMemoryFileTree().Deny("/srv");
        var query = CreateQuery("x", 8, 100);

        var events = await SearchAsync(tree, query);

        var summary = Summary(events);
        summary.Outcome.Should().Be(FileSearchOutcome.Failed);
        summary.Message.Should().Contain("Permission denied");
    }

    [TestMethod]
    public async Task Search_SymbolicLinkToDirectory_IsMatchedButNotFollowed()
    {
        var link = new FileEntry("loop", "/srv/loop", FileEntryKind.SymbolicLink) { LinkTargetKind = FileEntryKind.Directory };
        var tree = new InMemoryFileTree().AddDirectory("/srv").AddEntry(link);
        var query = CreateQuery("loop", 8, 100);

        var events = await SearchAsync(tree, query);

        events.OfType<FileSearchMatch>().Should().ContainSingle();
        tree.ListedPaths.Should().Equal("/srv");
    }

    private static FileSearchQuery CreateQuery(string pattern, int maxDepth, int maxResults)
    {
        return CreateQuery(pattern, maxDepth, maxResults, GenerousTimeLimit);
    }

    private static FileSearchQuery CreateQuery(string pattern, int maxDepth, int maxResults, TimeSpan timeLimit)
    {
        var namePattern = FileNamePattern.Create(pattern).Value;
        var limits = new FileSearchLimits(maxDepth, maxResults, timeLimit);
        var criteria = new FileSearchCriteria(namePattern, limits);
        return new FileSearchQuery("/srv", criteria);
    }

    private async Task<List<FileSearchEvent>> SearchAsync(InMemoryFileTree tree, FileSearchQuery query)
    {
        var searcher = new FileTreeSearcher(TimeProvider.System);
        var events = searcher.SearchAsync(tree, query, TestContext.CancellationToken);
        return await events.ToListAsync(CancellationToken.None);
    }

    private static FileSearchSummary Summary(List<FileSearchEvent> events)
    {
        events.Last().Should().BeOfType<FileSearchCompleted>();
        return ((FileSearchCompleted)events.Last()).Summary;
    }
}
