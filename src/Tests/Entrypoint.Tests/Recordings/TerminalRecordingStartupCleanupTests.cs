using DTech.Core.Terminal;
using DTech.Core.Terminal.Recording;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.Recordings;

[TestClass]
public sealed class TerminalRecordingStartupCleanupTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Startup_RemovesOwnedOrphansBeforeTheFirstHttpRequest()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"termforge-recording-startup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var options = new TerminalRecordingStorageOptions(directory);
            var storage = new TerminalRecordingStorage(options);
            var orphan = TerminalRecordingId.Create();
            await using (var cast = storage.CreateCast(orphan))
            {
                var content = "partial cast"u8.ToArray();
                await cast.WriteAsync(content, TestContext.CancellationToken);
            }

            var temporaryId = TerminalRecordingId.Create();
            var temporaryPath = Path.Combine(directory, temporaryId.Value + ".json.tmp");
            await File.WriteAllTextAsync(temporaryPath, "unfinished metadata", TestContext.CancellationToken);
            var complete = new TerminalRecordingMetadata
            {
                Id = TerminalRecordingId.Create(),
                ConnectionId = Guid.NewGuid(),
                ConnectionName = "server",
                SessionNumber = 1,
                Size = new TerminalSize(80, 24),
                StartedAt = DateTime.UtcNow,
            };
            await using (var cast = storage.CreateCast(complete.Id))
            {
                var header = AsciicastFormat.Header(complete);
                await cast.WriteAsync(header, TestContext.CancellationToken);
            }

            await storage.WriteMetadataAsync(complete, TestContext.CancellationToken);
            var settings = new Dictionary<string, string?> { ["Recordings:Directory"] = directory };
            await using var factory = new TermForgeApplicationFactory(settings);
            using var client = factory.CreateClientWithoutCookies();

            var orphanPath = Path.Combine(directory, orphan.Value + AsciicastFormat.FileExtension);
            var completePath = Path.Combine(directory, complete.Id.Value + AsciicastFormat.FileExtension);
            File.Exists(orphanPath).Should().BeFalse();
            File.Exists(temporaryPath).Should().BeFalse();
            File.Exists(completePath).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
