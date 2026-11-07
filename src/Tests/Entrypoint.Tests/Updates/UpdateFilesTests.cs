using System.Formats.Tar;
using System.IO.Compression;
using DTech.Entrypoint.Updates.Installation;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.Updates;

[TestClass]
public sealed class UpdateFilesTests
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"termforge-update-test-{Guid.NewGuid():N}");

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [TestMethod]
    public void Snapshot_restores_files_and_removes_files_created_after_update()
    {
        Directory.CreateDirectory(_directory);
        var database = Path.Combine(_directory, "data.db");
        var recordings = Path.Combine(_directory, "recordings");
        var snapshot = Path.Combine(_directory, "snapshot");
        File.WriteAllText(database, "old database");
        var targets = new[] { new UpdateDataPath(database, false), new UpdateDataPath(recordings, true) };
        UpdateFiles.SaveData(targets, snapshot);
        File.WriteAllText(database, "new database");
        Directory.CreateDirectory(recordings);
        File.WriteAllText(Path.Combine(recordings, "new.cast"), "recording");

        UpdateFiles.RestoreData(targets, snapshot);

        File.ReadAllText(database).Should().Be("old database");
        Directory.Exists(recordings).Should().BeFalse();
    }

    [TestMethod]
    public void Snapshot_rejects_symbolic_links()
    {
        if (OperatingSystem.IsWindows()) Assert.Inconclusive("Creating Windows links requires developer mode or elevated permissions.");
        Directory.CreateDirectory(_directory);
        var source = Path.Combine(_directory, "source");
        Directory.CreateDirectory(source);
        var target = Path.Combine(_directory, "target");
        File.WriteAllText(target, "secret");
        File.CreateSymbolicLink(Path.Combine(source, "link"), target);
        var destination = Path.Combine(_directory, "copy");

        var copy = () => UpdateFiles.CopyDirectory(source, destination);

        copy.Should().Throw<InvalidDataException>();
    }

    [TestMethod]
    public async Task Zip_rejects_path_traversal_without_writing_outside_staging()
    {
        Directory.CreateDirectory(_directory);
        var archive = Path.Combine(_directory, "package.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("../escaped");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("bad");
        }
        var staging = Path.Combine(_directory, "staging");
        var extract = () => ExtractAsync(archive, staging, "win-x64");

        await extract.Should().ThrowAsync<InvalidDataException>();
        File.Exists(Path.Combine(_directory, "escaped")).Should().BeFalse();
    }

    [TestMethod]
    public async Task Tar_rejects_symbolic_links()
    {
        Directory.CreateDirectory(_directory);
        var archive = Path.Combine(_directory, "package.tar.gz");
        using (var file = File.Create(archive))
        using (var gzip = new GZipStream(file, CompressionMode.Compress))
        using (var tar = new TarWriter(gzip))
        {
            var entry = new PaxTarEntry(TarEntryType.SymbolicLink, "termforge-1.0.0-linux-x64/link") { LinkName = "/etc/passwd" };
            tar.WriteEntry(entry);
        }
        var staging = Path.Combine(_directory, "staging");
        var extract = () => ExtractAsync(archive, staging, "linux-x64");

        await extract.Should().ThrowAsync<InvalidDataException>();
    }

    [TestMethod]
    public async Task Zip_extracts_only_the_expected_package()
    {
        Directory.CreateDirectory(_directory);
        var archive = Path.Combine(_directory, "package.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            string[] names = ["TermForge.exe", "wwwroot/index.html", "appsettings.json", "TermForge.staticwebassets.endpoints.json"];
            for (var i = 0; i < names.Length; i++)
            {
                var entry = zip.CreateEntry($"termforge-1.0.0-win-x64/{names[i]}");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("content");
            }
        }
        var staging = Path.Combine(_directory, "staging");

        await ExtractAsync(archive, staging, "win-x64");

        File.Exists(Path.Combine(staging, "TermForge.exe")).Should().BeTrue();
        File.Exists(Path.Combine(staging, "wwwroot", "index.html")).Should().BeTrue();
        File.Exists(Path.Combine(staging, "TermForge.staticwebassets.endpoints.json")).Should().BeTrue();
    }

    [TestMethod]
    public void Journal_resolves_macos_system_aliases_consistently_with_the_apphost()
    {
        if (!OperatingSystem.IsMacOS()) return;
        var alias = new UpdateJournal("/tmp/termforge-update-alias-test");
        var canonical = new UpdateJournal("/private/tmp/termforge-update-alias-test");

        alias.InstallationDirectory.Should().Be(canonical.InstallationDirectory);
        alias.Root.Should().Be(canonical.Root);
    }

    [TestMethod]
    [DataRow(".. ")]
    [DataRow("folder.")]
    [DataRow("folder ")]
    [DataRow("folder:stream")]
    public async Task Zip_rejects_ambiguous_windows_paths(string segment)
    {
        Directory.CreateDirectory(_directory);
        var archive = Path.Combine(_directory, "package.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry($"termforge-1.0.0-win-x64/wwwroot/{segment}/escaped");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("bad");
        }
        var staging = Path.Combine(_directory, "staging");
        var extract = () => ExtractAsync(archive, staging, "win-x64");

        await extract.Should().ThrowAsync<InvalidDataException>();
    }

    [TestMethod]
    public void Cancelling_an_aborted_preparation_does_not_leave_a_dangling_active_pointer()
    {
        var installation = Path.Combine(_directory, "application");
        var journal = new UpdateJournal(installation);
        var job = new UpdateJob
        {
            Id = Guid.NewGuid().ToString("N"), InstallationDirectory = journal.InstallationDirectory,
            WorkingDirectory = _directory, CurrentVersion = "1.0.0", TargetVersion = "1.1.0",
            RuntimeIdentifier = "linux-x64", DataPaths = [], Arguments = [], Phase = "aborted",
        };
        journal.Activate(job);

        journal.DeactivateAborted(job.Id);
        UpdateFiles.Delete(journal.JobDirectory(job.Id));

        journal.Active().Should().BeNull();
    }

    private static Task ExtractAsync(string archive, string staging, string rid)
    {
        var request = new UpdateArchive.Extraction(archive, staging, $"termforge-1.0.0-{rid}/", rid.StartsWith("win-", StringComparison.Ordinal));
        return UpdateArchive.ExtractAsync(request, CancellationToken.None);
    }
}
