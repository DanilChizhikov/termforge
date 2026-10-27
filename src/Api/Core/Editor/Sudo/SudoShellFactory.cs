namespace DTech.Core.Editor.Sudo;

public sealed class SudoShellFactory(SudoCommandBuilder commands, EditorLimits limits)
{
    public SudoShell Create(IRemoteShellCommandRunner runner, SudoPasswordInput? password)
    {
        return new SudoShell(runner, commands, password, limits);
    }

    public SudoRemoteEditorFileClient CreateClient(SudoShell shell)
    {
        return new SudoRemoteEditorFileClient(shell, commands);
    }
}
