using DTech.Core.CommandHistory;
using DTech.Core.SavedConnections;
using DTech.Persistence.CommandHistory;
using DTech.Persistence.SavedConnections;
using DTech.Persistence.Tests.Infrastructure;
using FluentAssertions;

namespace DTech.Persistence.Tests.CommandHistory;

[TestClass]
public sealed class PinnedCommandImportTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ImportPinned_AddsAndPinsWithoutDuplicatesOutsideTheHistoryPageLimit()
    {
        using var database = new TemporaryDatabase();
        await database.MigrateAsync();
        await using var context = database.CreateContext();
        var connectionId = Guid.NewGuid();
        var details = new SavedConnectionDraft { Name = "web", Host = "web.test", UserName = "deploy" };
        var connection = new SavedConnection { Id = connectionId, Details = details };
        var connections = new SavedConnectionRepository(context);
        await connections.AddAsync(connection, TestContext.CancellationToken);
        var repository = new CommandHistoryRepository(context);
        var oldest = new CommandHistoryEntry(Guid.NewGuid(), connectionId, "old command", DateTime.UtcNow.AddDays(-1), 0, false);
        await repository.AddAsync(oldest, TestContext.CancellationToken);
        var entries = new List<CommandHistoryEntry>();
        for (var i = 0; i < 1200; i++)
        {
            var entry = new CommandHistoryEntry(Guid.NewGuid(), connectionId, $"command {i}", DateTime.UtcNow, null, true);
            entries.Add(entry);
        }

        var pinOld = oldest with { Id = Guid.NewGuid(), IsPinned = true };
        entries.Add(pinOld);
        entries.Add(entries[0] with { Id = Guid.NewGuid() });
        var saves = 0;
        context.SavingChanges += (_, _) => saves++;

        var added = await repository.ImportPinnedAsync(entries, TestContext.CancellationToken);
        var repeated = await repository.ImportPinnedAsync(entries, TestContext.CancellationToken);

        added.Should().Be(1201);
        repeated.Should().Be(0);
        saves.Should().Be(1);
        var rows = database.Query("SELECT Command, IsPinned FROM command_history");
        rows.Should().HaveCount(1201);
        rows.Should().OnlyContain(row => Convert.ToInt64(row["IsPinned"]) == 1);
    }
}
