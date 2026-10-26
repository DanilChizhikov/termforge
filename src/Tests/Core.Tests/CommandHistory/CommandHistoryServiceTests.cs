using DTech.Core.CommandHistory;
using DTech.Core.Tests.CommandHistory.Support;
using FluentAssertions;

namespace DTech.Core.Tests.CommandHistory;

[TestClass]
public sealed class CommandHistoryServiceTests
{
    private static readonly Guid WebId = Guid.NewGuid();
    private static readonly Guid DbId = Guid.NewGuid();
    private static readonly DateTime Start = new(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);

    private InMemoryCommandHistoryRepository _repository = null!;
    private InMemoryExclusionPatternStore _patternStore = null!;
    private CommandExclusionSettings _settings = null!;
    private CommandHistoryService _service = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _repository = new InMemoryCommandHistoryRepository();
        _patternStore = new InMemoryExclusionPatternStore();
        _settings = new CommandExclusionSettings(_patternStore);
        _service = new CommandHistoryService(_repository, _settings);
    }

    [TestMethod]
    public async Task Record_StoresCommandWithTimestampAndExitCodeButNoOutput()
    {
        var recorded = await _service.RecordAsync(new RecordedCommand(WebId, "ls -la\n", Start, 0), TestContext.CancellationToken);

        recorded.Should().BeTrue();
        var entry = _repository.Snapshot.Should().ContainSingle().Subject;
        entry.Command.Should().Be("ls -la");
        entry.ConnectionId.Should().Be(WebId);
        entry.ExecutedAt.Should().Be(Start);
        entry.ExitCode.Should().Be(0);
        entry.IsPinned.Should().BeFalse();
    }

    [TestMethod]
    public async Task Record_WithoutExitCode_KeepsItUnknown()
    {
        await _service.RecordAsync(new RecordedCommand(WebId, "sleep 1", Start, null), TestContext.CancellationToken);

        _repository.Snapshot.Single().ExitCode.Should().BeNull();
    }

    [TestMethod]
    public async Task Record_CommandWithLeadingSpace_IsNotStored()
    {
        var recorded = await _service.RecordAsync(new RecordedCommand(WebId, " mysql -h db", Start, 0), TestContext.CancellationToken);

        recorded.Should().BeFalse();
        _repository.Snapshot.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Record_CommandMatchingDefaultSecretPattern_IsNotStored()
    {
        var recorded = await _service.RecordAsync(new RecordedCommand(WebId, "export API_TOKEN=abc", Start, 0), TestContext.CancellationToken);

        recorded.Should().BeFalse();
        _repository.Snapshot.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Record_UsesConfiguredPatternsInsteadOfDefaults()
    {
        await _settings.SavePatternsAsync([@"^internal-tool\b"], TestContext.CancellationToken);

        var secret = await _service.RecordAsync(new RecordedCommand(WebId, "internal-tool login", Start, 0), TestContext.CancellationToken);
        var token = await _service.RecordAsync(new RecordedCommand(WebId, "export TOKEN=abc", Start, 0), TestContext.CancellationToken);

        secret.Should().BeFalse();
        token.Should().BeTrue();
    }

    [TestMethod]
    public async Task Record_KeepsOnlyNewestUnpinnedEntriesAndNeverPrunesPinned()
    {
        await _service.RecordAsync(new RecordedCommand(WebId, "keep-me", Start, 0), TestContext.CancellationToken);
        var pinnedId = _repository.Snapshot.Single().Id;
        await _service.SetPinnedAsync(pinnedId, true, TestContext.CancellationToken);

        for (var i = 1; i <= CommandHistoryLimits.MaxUnpinnedEntriesPerConnection + 5; i++)
        {
            await _service.RecordAsync(new RecordedCommand(WebId, $"cmd-{i}", Start.AddMinutes(i), 0), TestContext.CancellationToken);
        }

        var all = _repository.Snapshot;
        all.Count(entry => !entry.IsPinned).Should().Be(CommandHistoryLimits.MaxUnpinnedEntriesPerConnection);
        all.Should().Contain(entry => entry.Id == pinnedId);
        all.Should().NotContain(entry => entry.Command == "cmd-1");
    }

    [TestMethod]
    public async Task List_PutsPinnedFirstThenNewestAndFiltersBySearch()
    {
        await _service.RecordAsync(new RecordedCommand(WebId, "docker ps", Start, 0), TestContext.CancellationToken);
        await _service.RecordAsync(new RecordedCommand(WebId, "ls", Start.AddMinutes(1), 0), TestContext.CancellationToken);
        await _service.RecordAsync(new RecordedCommand(WebId, "docker logs api", Start.AddMinutes(2), 1), TestContext.CancellationToken);
        await _service.RecordAsync(new RecordedCommand(DbId, "psql", Start.AddMinutes(3), 0), TestContext.CancellationToken);
        var oldest = _repository.Snapshot.Single(entry => entry.Command == "docker ps");
        await _service.SetPinnedAsync(oldest.Id, true, TestContext.CancellationToken);

        var all = await _service.ListAsync(new CommandHistoryQuery(WebId, null, 50), TestContext.CancellationToken);
        var searched = await _service.ListAsync(new CommandHistoryQuery(WebId, "  DOCKER ", 50), TestContext.CancellationToken);

        all.Select(entry => entry.Command).Should().Equal("docker ps", "docker logs api", "ls");
        searched.Select(entry => entry.Command).Should().Equal("docker ps", "docker logs api");
    }

    [TestMethod]
    public async Task Pin_TogglesAndReportsUnknownEntries()
    {
        await _service.RecordAsync(new RecordedCommand(WebId, "uptime", Start, 0), TestContext.CancellationToken);
        var id = _repository.Snapshot.Single().Id;

        var pinned = await _service.SetPinnedAsync(id, true, TestContext.CancellationToken);
        var unpinned = await _service.SetPinnedAsync(id, false, TestContext.CancellationToken);
        var unknown = await _service.SetPinnedAsync(Guid.NewGuid(), true, TestContext.CancellationToken);

        pinned.Should().BeTrue();
        unpinned.Should().BeTrue();
        unknown.Should().BeFalse();
        _repository.Snapshot.Single().IsPinned.Should().BeFalse();
    }

    [TestMethod]
    public async Task ClearUnpinned_KeepsPinnedEntriesAndOtherServers()
    {
        await _service.RecordAsync(new RecordedCommand(WebId, "a", Start, 0), TestContext.CancellationToken);
        await _service.RecordAsync(new RecordedCommand(WebId, "b", Start.AddMinutes(1), 0), TestContext.CancellationToken);
        await _service.RecordAsync(new RecordedCommand(DbId, "c", Start.AddMinutes(2), 0), TestContext.CancellationToken);
        var pinned = _repository.Snapshot.Single(entry => entry.Command == "a");
        await _service.SetPinnedAsync(pinned.Id, true, TestContext.CancellationToken);

        var removed = await _service.ClearUnpinnedAsync(WebId, TestContext.CancellationToken);

        removed.Should().Be(1);
        _repository.Snapshot.Select(entry => entry.Command).Should().BeEquivalentTo(["a", "c"]);
    }

    [TestMethod]
    public async Task ExclusionSettings_ValidateAndFallBackToDefaultsUntilSaved()
    {
        var initial = await _settings.GetPatternsAsync(TestContext.CancellationToken);
        var invalid = await _settings.SavePatternsAsync(["(unclosed"], TestContext.CancellationToken);
        await _settings.SavePatternsAsync(["custom"], TestContext.CancellationToken);
        var saved = await _settings.GetPatternsAsync(TestContext.CancellationToken);
        await _settings.ResetAsync(TestContext.CancellationToken);
        var reset = await _settings.GetPatternsAsync(TestContext.CancellationToken);

        initial.Should().Equal(DefaultCommandExclusionPatterns.Values);
        invalid.Should().Contain("not a valid regular expression");
        saved.Should().Equal("custom");
        reset.Should().Equal(DefaultCommandExclusionPatterns.Values);
    }
}
