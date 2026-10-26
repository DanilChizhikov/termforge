using DTech.Core.CommandHistory;
using DTech.Core.SavedConnections;
using DTech.Persistence.CommandHistory;
using DTech.Persistence.SavedConnections;
using DTech.Persistence.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DTech.Persistence.Tests.CommandHistory;

[TestClass]
public sealed class CommandHistoryRepositoryTests
{
    private static readonly DateTime Start = new(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);

    private TemporaryDatabase _database = null!;
    private Guid _webId;
    private Guid _dbId;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _database = new TemporaryDatabase();
        await _database.MigrateAsync();
        _webId = await AddConnectionAsync("web");
        _dbId = await AddConnectionAsync("db");
    }

    [TestCleanup]
    public void Cleanup()
    {
        _database.Dispose();
    }

    [TestMethod]
    public async Task AddAndList_RoundTripsEveryFieldWithUtcTimestamp()
    {
        var entry = new CommandHistoryEntry(Guid.NewGuid(), _webId, "ls -la", Start, 127, false);

        await Run(repository => repository.AddAsync(entry, TestContext.CancellationToken));
        var listed = await ListAsync(new CommandHistoryQuery(_webId, null, 10));

        var stored = listed.Should().ContainSingle().Subject;
        stored.Should().Be(entry);
        stored.ExecutedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [TestMethod]
    public async Task Add_WithoutExitCode_StoresNull()
    {
        var entry = new CommandHistoryEntry(Guid.NewGuid(), _webId, "sleep 5", Start, null, false);

        await Run(repository => repository.AddAsync(entry, TestContext.CancellationToken));

        (await ListAsync(new CommandHistoryQuery(_webId, null, 10))).Single().ExitCode.Should().BeNull();
    }

    [TestMethod]
    public async Task List_IsScopedToConnectionPinnedFirstThenNewestFirstAndLimited()
    {
        var oldPinned = await AddAsync(_webId, "old pinned", Start, true);
        await AddAsync(_webId, "middle", Start.AddMinutes(1), false);
        await AddAsync(_webId, "newest", Start.AddMinutes(2), false);
        await AddAsync(_dbId, "other server", Start.AddMinutes(3), false);

        var all = await ListAsync(new CommandHistoryQuery(_webId, null, 10));
        var limited = await ListAsync(new CommandHistoryQuery(_webId, null, 2));

        all.Select(entry => entry.Command).Should().Equal("old pinned", "newest", "middle");
        limited.Select(entry => entry.Command).Should().Equal("old pinned", "newest");
        all[0].Id.Should().Be(oldPinned);
    }

    [TestMethod]
    public async Task List_Search_IsCaseInsensitiveAndTreatsWildcardsLiterally()
    {
        await AddAsync(_webId, "Docker PS", Start, false);
        await AddAsync(_webId, "echo 100%", Start.AddMinutes(1), false);
        await AddAsync(_webId, "echo 1000", Start.AddMinutes(2), false);
        await AddAsync(_webId, "ls my_dir", Start.AddMinutes(3), false);
        await AddAsync(_webId, "ls myXdir", Start.AddMinutes(4), false);

        var docker = await ListAsync(new CommandHistoryQuery(_webId, "docker", 10));
        var percent = await ListAsync(new CommandHistoryQuery(_webId, "100%", 10));
        var underscore = await ListAsync(new CommandHistoryQuery(_webId, "my_dir", 10));

        docker.Select(entry => entry.Command).Should().Equal("Docker PS");
        percent.Select(entry => entry.Command).Should().Equal("echo 100%");
        underscore.Select(entry => entry.Command).Should().Equal("ls my_dir");
    }

    [TestMethod]
    public async Task SetPinned_PersistsAndReportsUnknownEntries()
    {
        var id = await AddAsync(_webId, "uptime", Start, false);

        var pinned = await UseRepository(repository => repository.SetPinnedAsync(id, true, TestContext.CancellationToken));
        var unknown = await UseRepository(repository => repository.SetPinnedAsync(Guid.NewGuid(), true, TestContext.CancellationToken));

        pinned.Should().BeTrue();
        unknown.Should().BeFalse();
        (await ListAsync(new CommandHistoryQuery(_webId, null, 10))).Single().IsPinned.Should().BeTrue();
    }

    [TestMethod]
    public async Task Delete_RemovesOnlyThatEntry()
    {
        var removed = await AddAsync(_webId, "remove me", Start, false);
        await AddAsync(_webId, "keep me", Start.AddMinutes(1), false);

        var deleted = await UseRepository(repository => repository.DeleteAsync(removed, TestContext.CancellationToken));

        deleted.Should().BeTrue();
        (await ListAsync(new CommandHistoryQuery(_webId, null, 10))).Select(entry => entry.Command).Should().Equal("keep me");
    }

    [TestMethod]
    public async Task ClearUnpinned_KeepsPinnedEntriesAndOtherConnections()
    {
        await AddAsync(_webId, "pinned", Start, true);
        await AddAsync(_webId, "plain", Start.AddMinutes(1), false);
        await AddAsync(_dbId, "other", Start.AddMinutes(2), false);

        var removed = await UseRepository(repository => repository.ClearUnpinnedAsync(_webId, TestContext.CancellationToken));

        removed.Should().Be(1);
        (await ListAsync(new CommandHistoryQuery(_webId, null, 10))).Select(entry => entry.Command).Should().Equal("pinned");
        (await ListAsync(new CommandHistoryQuery(_dbId, null, 10))).Should().HaveCount(1);
    }

    [TestMethod]
    public async Task PruneUnpinned_KeepsNewestAndEveryPinnedEntry()
    {
        await AddAsync(_webId, "pinned oldest", Start, true);
        await AddAsync(_webId, "one", Start.AddMinutes(1), false);
        await AddAsync(_webId, "two", Start.AddMinutes(2), false);
        await AddAsync(_webId, "three", Start.AddMinutes(3), false);
        await AddAsync(_dbId, "other", Start.AddMinutes(4), false);

        var removed = await UseRepository(repository => repository.PruneUnpinnedAsync(_webId, 2, TestContext.CancellationToken));

        removed.Should().Be(1);
        var remaining = await ListAsync(new CommandHistoryQuery(_webId, null, 10));
        remaining.Select(entry => entry.Command).Should().Equal("pinned oldest", "three", "two");
        (await ListAsync(new CommandHistoryQuery(_dbId, null, 10))).Should().HaveCount(1);
    }

    [TestMethod]
    public async Task DeletingTheConnection_DeletesItsHistory()
    {
        await AddAsync(_webId, "ls", Start, false);
        await AddAsync(_dbId, "psql", Start, false);

        using (var scope = _database.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<Persistence.Database.TermForgeDbContext>();
            var connections = new SavedConnectionRepository(context);
            await connections.DeleteAsync(_webId, TestContext.CancellationToken);
        }

        var rows = _database.Query("SELECT connection_id FROM command_history");
        rows.Should().HaveCount(1);
    }

    [TestMethod]
    public void Table_StoresCommandTextButHasNoColumnForTerminalOutput()
    {
        var columns = _database.Query("SELECT name FROM pragma_table_info('command_history')")
            .Select(row => (string)row["name"]!)
            .ToList();

        columns.Should().BeEquivalentTo("id", "connection_id", "command", "executed_at", "exit_code", "is_pinned");
    }

    [TestMethod]
    public async Task ExclusionPatternStore_ReturnsNullUntilSavedThenRoundTrips()
    {
        var before = await UseStore(store => store.LoadAsync(TestContext.CancellationToken));
        await UseStore(async store =>
        {
            await store.SaveAsync(["^deploy\\b", "secret"], TestContext.CancellationToken);
            return true;
        });
        await UseStore(async store =>
        {
            await store.SaveAsync(["only"], TestContext.CancellationToken);
            return true;
        });
        var after = await UseStore(store => store.LoadAsync(TestContext.CancellationToken));

        before.Should().BeNull();
        after.Should().Equal("only");
    }

    private async Task<Guid> AddConnectionAsync(string name)
    {
        var details = new SavedConnectionDraft { Name = name, Host = "example.org", UserName = "deploy" };
        var connection = new SavedConnection { Id = Guid.NewGuid(), Details = details, CreatedAt = Start, UpdatedAt = Start };
        using var scope = _database.Services.CreateScope();
        var repository = new SavedConnectionRepository(scope.ServiceProvider.GetRequiredService<Persistence.Database.TermForgeDbContext>());
        await repository.AddAsync(connection, TestContext.CancellationToken);
        return connection.Id;
    }

    private async Task<Guid> AddAsync(Guid connectionId, string command, DateTime executedAt, bool isPinned)
    {
        var entry = new CommandHistoryEntry(Guid.NewGuid(), connectionId, command, executedAt, 0, isPinned);
        await Run(repository => repository.AddAsync(entry, TestContext.CancellationToken));
        return entry.Id;
    }

    private Task<IReadOnlyList<CommandHistoryEntry>> ListAsync(CommandHistoryQuery query)
    {
        return UseRepository(repository => repository.ListAsync(query, TestContext.CancellationToken));
    }

    private async Task<TResult> UseRepository<TResult>(Func<CommandHistoryRepository, Task<TResult>> action)
    {
        using var scope = _database.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Persistence.Database.TermForgeDbContext>();
        return await action(new CommandHistoryRepository(context));
    }

    private async Task Run(Func<CommandHistoryRepository, Task> action)
    {
        using var scope = _database.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Persistence.Database.TermForgeDbContext>();
        await action(new CommandHistoryRepository(context));
    }

    private async Task<TResult> UseStore<TResult>(Func<CommandExclusionPatternStore, Task<TResult>> action)
    {
        using var scope = _database.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Persistence.Database.TermForgeDbContext>();
        var store = new CommandExclusionPatternStore(context, TimeProvider.System);
        return await action(store);
    }
}
