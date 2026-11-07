using System.Diagnostics;
using System.Runtime.InteropServices;
using DTech.Core.Terminal.Recording;
using DTech.Core.Updates;
using DTech.Credentials.Vault;
using DTech.Persistence.Database;

namespace DTech.Entrypoint.Updates.Installation;

internal sealed class UpdateInstallation
{
    private readonly DatabasePathResolver _database;
    private readonly VaultFile _vault;
    private readonly TerminalRecordingStorageOptions _recordings;
    private readonly ICurrentVersionProvider _version;

    public UpdateInstallation(DatabasePathResolver database, VaultFile vault, TerminalRecordingStorageOptions recordings, ICurrentVersionProvider version)
    {
        _database = database;
        _vault = vault;
        _recordings = recordings;
        _version = version;
    }

    public UpdateJournal Journal { get; } = new(AppContext.BaseDirectory);

    public string? UnavailableReason
    {
        get
        {
            var executable = Path.Combine(Journal.InstallationDirectory, UpdateProcess.ExecutableName);
            var assembly = Path.Combine(Journal.InstallationDirectory, "TermForge.dll");
            var processPath = Environment.ProcessPath is { } path ? UpdateFiles.NormalizePath(path) : null;
            if (File.Exists(assembly) || processPath != executable)
                return "Install updates from a published TermForge package. SDK and development launches cannot replace themselves.";
            if (!File.Exists(Path.Combine(Journal.InstallationDirectory, "wwwroot", "index.html")))
                return "The published web interface is missing.";
            return null;
        }
    }

    public UpdateJob Create(string targetVersion)
    {
        if (UnavailableReason is { } reason) throw new InvalidOperationException(reason);
        using var process = Process.GetCurrentProcess();
        var database = _database.Resolve();
        var paths = new List<UpdateDataPath>
        {
            new(database, false), new($"{database}-wal", false), new($"{database}-shm", false),
            new(_vault.FilePath, false), new(Path.GetFullPath(_recordings.DirectoryPath), true),
        };
        var configurations = Directory.GetFiles(Journal.InstallationDirectory, "appsettings*.json");
        for (var i = 0; i < configurations.Length; i++) paths.Add(new UpdateDataPath(configurations[i], false));
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var normalized = paths.DistinctBy(path => path.Path, comparer).ToArray();
        for (var i = 0; i < normalized.Length; i++)
        {
            UpdateFiles.RejectLinksInPath(normalized[i].Path);
            var rootPrefix = Journal.Root + Path.DirectorySeparatorChar;
            if (normalized[i].Path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Application data must be outside the update backup directory.");
            if (normalized[i].Directory && Journal.Root.StartsWith(normalized[i].Path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("The recordings directory cannot contain the installation or its update backups.");
            for (var j = 0; j < normalized.Length; j++)
            {
                if (i == j || !normalized[i].Directory) continue;
                if (normalized[j].Path.StartsWith(normalized[i].Path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Overlapping data paths cannot be backed up safely.");
            }
        }
        var args = Environment.GetCommandLineArgs()[1..];
        return new UpdateJob
        {
            Id = Guid.NewGuid().ToString("N"), InstallationDirectory = Journal.InstallationDirectory,
            WorkingDirectory = Environment.CurrentDirectory, Arguments = args,
            CurrentVersion = _version.Current.ToString(), TargetVersion = targetVersion,
            RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier, DataPaths = normalized,
            ParentProcessId = process.Id, ParentStartTicks = process.StartTime.ToUniversalTime().Ticks,
        };
    }

    public void Preflight(UpdateJob job)
    {
        UpdateFiles.RejectLinksInPath(job.InstallationDirectory);
        var root = Journal.Root;
        UpdateFiles.CreatePrivateDirectory(root);
        var probe = Path.Combine(job.InstallationDirectory, $".update-probe-{job.Id}");
        using (File.Create(probe)) { }
        File.Delete(probe);
        var applicationBytes = UpdateFiles.Size(job.InstallationDirectory);
        long dataBytes = 0;
        for (var i = 0; i < job.DataPaths.Length; i++) dataBytes = checked(dataBytes + UpdateFiles.Size(job.DataPaths[i].Path));
        UpdateFiles.RequireSpace(root, applicationBytes * 3 + dataBytes * 2);
    }
}
