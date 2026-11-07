using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DTech.Entrypoint.Updates.Installation;

internal sealed class UpdateJournal(string installationDirectory)
{
    public string InstallationDirectory { get; } = Path.TrimEndingDirectorySeparator(UpdateFiles.NormalizePath(installationDirectory));

    public string Root
    {
        get
        {
            var identity = OperatingSystem.IsWindows() ? InstallationDirectory.ToUpperInvariant() : InstallationDirectory;
            var bytes = Encoding.UTF8.GetBytes(identity);
            var hash = Convert.ToHexString(SHA256.HashData(bytes))[..16];
            return Path.Combine(Path.GetDirectoryName(InstallationDirectory)!, $".termforge-updates-{hash}");
        }
    }

    public string JobDirectory(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException("Invalid update identifier.");
        return Path.Combine(Root, id);
    }

    public UpdateJob? Active() => ReadPointer("active.json");

    public UpdateJob? Previous() => ReadPointer("previous.json");

    public UpdateJob Read(string id)
    {
        var path = Path.Combine(JobDirectory(id), "job.json");
        UpdateFiles.RejectLinksInPath(path);
        var job = JsonSerializer.Deserialize<UpdateJob>(File.ReadAllText(path)) ?? throw new InvalidDataException("Invalid update journal.");
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (job.Id != id || !string.Equals(job.InstallationDirectory, InstallationDirectory, comparison))
            throw new InvalidDataException("The update journal belongs to another installation.");
        return job;
    }

    public void Save(UpdateJob job)
    {
        var path = Path.Combine(JobDirectory(job.Id), "job.json");
        Write(path, job);
    }

    public void Activate(UpdateJob job)
    {
        Save(job);
        Write(Path.Combine(Root, "active.json"), job.Id);
    }

    public void SetPrevious(UpdateJob? job)
    {
        var path = Path.Combine(Root, "previous.json");
        if (job is null) File.Delete(path);
        else Write(path, job.Id);
    }

    public void DeactivateAborted(string id)
    {
        var active = Active();
        if (active?.Id != id) return;
        if (active.Phase != "aborted") throw new InvalidOperationException("An active update cannot be discarded.");
        File.Delete(Path.Combine(Root, "active.json"));
    }

    public bool NeedsRecovery(UpdateJob? job) => job is not null && job.Phase is not ("completed" or "cancelled" or "aborted");

    public void MarkReady(string id)
    {
        var path = Path.Combine(JobDirectory(id), "ready");
        Write(path, id);
    }

    private UpdateJob? ReadPointer(string name)
    {
        var path = Path.Combine(Root, name);
        UpdateFiles.RejectLinksInPath(path);
        if (!File.Exists(path)) return null;
        var id = JsonSerializer.Deserialize<string>(File.ReadAllText(path)) ?? throw new InvalidDataException("Invalid update pointer.");
        return Read(id);
    }

    private static void Write<T>(string path, T value)
    {
        var parent = Path.GetDirectoryName(path)!;
        UpdateFiles.CreatePrivateDirectory(parent);
        UpdateFiles.RejectLinksInPath(path);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, Options = FileOptions.WriteThrough };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        try
        {
            using (var stream = new FileStream(temporary, options))
            {
                JsonSerializer.Serialize(stream, value);
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally { File.Delete(temporary); }
    }
}
