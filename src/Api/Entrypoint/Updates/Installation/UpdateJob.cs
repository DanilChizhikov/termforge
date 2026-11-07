namespace DTech.Entrypoint.Updates.Installation;

internal sealed record UpdateDataPath(string Path, bool Directory);

internal sealed record UpdateJob
{
    public required string Id { get; init; }
    public required string InstallationDirectory { get; init; }
    public required string WorkingDirectory { get; init; }
    public required string[] Arguments { get; init; }
    public required string CurrentVersion { get; init; }
    public required string TargetVersion { get; init; }
    public required string RuntimeIdentifier { get; init; }
    public required UpdateDataPath[] DataPaths { get; init; }
    public string Mode { get; init; } = "update";
    public string Phase { get; init; } = "prepared";
    public string? Error { get; init; }
    public string? RestoreId { get; init; }
    public string? PreviousId { get; init; }
    public bool SaveCurrentData { get; init; } = true;
    public int ParentProcessId { get; init; }
    public long ParentStartTicks { get; init; }
    public int ChildProcessId { get; init; }
    public long ChildStartTicks { get; init; }
    public int StartupTimeoutSeconds { get; init; } = 60;
    public bool DataBackupComplete { get; init; }
}

internal sealed record InstallationStatus(string State, string? Version, string? Error, bool Supported, string? UnavailableReason, string? PreviousVersion, bool ReadyToRestart = false, string? Operation = null);

internal sealed record InstallUpdateRequest(bool WaitForTransfers = true, bool SaveCurrentData = true);

internal sealed record RollbackUpdateRequest(bool SaveCurrentData = true);
