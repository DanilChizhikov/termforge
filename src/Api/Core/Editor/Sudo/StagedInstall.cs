namespace DTech.Core.Editor.Sudo;

public readonly record struct StagedInstall(string StagedPath, string TargetPath, int Mode, RemoteFileOwner Owner);
