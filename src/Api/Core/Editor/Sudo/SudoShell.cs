using DTech.Core.Connections;

namespace DTech.Core.Editor.Sudo;

public sealed class SudoShell : IAsyncDisposable
{
    private const long OutputAllowanceBytes = 64 * 1024;

    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(1);

    private readonly IRemoteShellCommandRunner _runner;
    private readonly SudoCommandBuilder _commands;
    private readonly SudoPasswordInput? _password;
    private readonly int _maxOutputBytes;

    public SudoShell(IRemoteShellCommandRunner runner, SudoCommandBuilder commands, SudoPasswordInput? password, EditorLimits limits)
    {
        _runner = runner;
        _commands = commands;
        _password = password;
        var maxOutputBytes = limits.MaxFileBytes + 1 + OutputAllowanceBytes;
        _maxOutputBytes = (int)Math.Min(maxOutputBytes, int.MaxValue);
    }

    public bool HasPassword => _password is not null;

    private ReadOnlyMemory<byte> PasswordLine => _password?.Line ?? ReadOnlyMemory<byte>.Empty;

    public Task<SshResult<RemoteShellCommandResult>> RunElevatedAsync(string command, CancellationToken cancellationToken)
    {
        var text = _commands.Elevate(HasPassword, command);
        return RunAsync(text, PasswordLine, cancellationToken);
    }

    public Task<SshResult<RemoteShellCommandResult>> RunElevatedWithCleanupAsync(string command, string stagedPath, CancellationToken cancellationToken)
    {
        var elevated = _commands.Elevate(HasPassword, command);
        var text = _commands.WithCleanup(elevated, stagedPath);
        return RunAsync(text, PasswordLine, cancellationToken);
    }

    public Task<SshResult<RemoteShellCommandResult>> RunAsUserAsync(string command, ReadOnlyMemory<byte> standardInput, CancellationToken cancellationToken)
    {
        return RunAsync(command, standardInput, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _password?.Dispose();
        await _runner.DisposeAsync();
    }

    private Task<SshResult<RemoteShellCommandResult>> RunAsync(string text, ReadOnlyMemory<byte> standardInput, CancellationToken cancellationToken)
    {
        var command = new RemoteShellCommand(text, CommandTimeout, _maxOutputBytes)
        {
            StandardInput = standardInput,
        };
        return _runner.RunAsync(command, cancellationToken);
    }
}
