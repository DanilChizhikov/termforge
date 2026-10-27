namespace DTech.Core.Editor.Sudo;

public sealed record ConnectedShellRunner(IRemoteShellCommandRunner Runner, string ServerName);
