using DTech.Core.Connections;
using DTech.Core.Files;

namespace DTech.Core.Editor.Sudo;

public sealed class SudoRemoteEditorFileClient(SudoShell shell, SudoCommandBuilder commands) : IRemoteEditorFileClient
{
    private const char NameSeparator = '\0';

    private static readonly RemoteFileOwner RootOwner = new(0, 0);

    public async Task<SshResult<RemoteFileAttributes>> GetAttributesAsync(string remotePath, CancellationToken cancellationToken)
    {
        var command = commands.Stat(remotePath);
        var action = $"Reading attributes of {remotePath}";
        var executed = await RunElevatedAsync(command, action, cancellationToken);
        if (!executed.IsSuccess)
        {
            return SshResult<RemoteFileAttributes>.Failure(executed.Error!);
        }

        var output = executed.Value.StandardOutputText;
        var attributes = StatOutputParser.Parse(output);
        if (attributes is null)
        {
            var unreadable = new SshError(SshErrorKind.OperationFailed, $"{action} failed: unexpected stat output.");
            return SshResult<RemoteFileAttributes>.Failure(unreadable);
        }

        return SshResult<RemoteFileAttributes>.Success(attributes);
    }

    public async Task<SshResult<byte[]>> ReadAsync(EditorReadRequest request, CancellationToken cancellationToken)
    {
        var byteCount = request.MaxBytes + 1;
        var command = commands.ReadHead(request.Path, byteCount);
        var executed = await RunElevatedAsync(command, $"Reading {request.Path}", cancellationToken);
        if (!executed.IsSuccess)
        {
            return SshResult<byte[]>.Failure(executed.Error!);
        }

        return SshResult<byte[]>.Success(executed.Value.StandardOutput);
    }

    public async Task<SshResult<RemoteFileAttributes>> CreateFileAsync(RemoteFileCreation creation, CancellationToken cancellationToken)
    {
        var staged = await StageAsync(creation.Content, cancellationToken);
        if (!staged.IsSuccess)
        {
            return SshResult<RemoteFileAttributes>.Failure(staged.Error!);
        }

        var owner = creation.Owner ?? RootOwner;
        var install = new StagedInstall(staged.Value, creation.Path, creation.Mode, owner);
        var command = commands.Install(install);
        var executed = await RunElevatedWithCleanupAsync(command, staged.Value, $"Writing {creation.Path}", cancellationToken);
        if (!executed.IsSuccess)
        {
            return SshResult<RemoteFileAttributes>.Failure(executed.Error!);
        }

        return await GetAttributesAsync(creation.Path, cancellationToken);
    }

    public async Task<SshResult> OverwriteAsync(RemoteFileContent content, CancellationToken cancellationToken)
    {
        var staged = await StageAsync(content.Content, cancellationToken);
        if (!staged.IsSuccess)
        {
            return SshResult.Failure(staged.Error!);
        }

        var command = commands.Overwrite(staged.Value, content.Path);
        var executed = await RunElevatedWithCleanupAsync(command, staged.Value, $"Writing {content.Path}", cancellationToken);
        return executed.IsSuccess ? SshResult.Success() : SshResult.Failure(executed.Error!);
    }

    public async Task<SshResult> ChangeOwnerAsync(RemoteOwnership ownership, CancellationToken cancellationToken)
    {
        var command = commands.ChangeOwner(ownership);
        var executed = await RunElevatedAsync(command, $"Changing the owner of {ownership.Path}", cancellationToken);
        return executed.IsSuccess ? SshResult.Success() : SshResult.Failure(executed.Error!);
    }

    public async Task<SshResult<AtomicReplaceOutcome>> ReplaceAtomicallyAsync(RemoteRenameRequest request, CancellationToken cancellationToken)
    {
        var command = commands.Move(request.FromPath, request.ToPath);
        var executed = await RunElevatedAsync(command, $"Replacing {request.ToPath}", cancellationToken);
        if (!executed.IsSuccess)
        {
            return SshResult<AtomicReplaceOutcome>.Failure(executed.Error!);
        }

        return SshResult<AtomicReplaceOutcome>.Success(AtomicReplaceOutcome.Replaced);
    }

    public async Task<SshResult> RenameAsync(RemoteRenameRequest request, CancellationToken cancellationToken)
    {
        var command = commands.Move(request.FromPath, request.ToPath);
        var executed = await RunElevatedAsync(command, $"Renaming {request.FromPath}", cancellationToken);
        return executed.IsSuccess ? SshResult.Success() : SshResult.Failure(executed.Error!);
    }

    public async Task<SshResult> DeleteFileAsync(string remotePath, CancellationToken cancellationToken)
    {
        var command = commands.Delete(remotePath);
        var executed = await RunElevatedAsync(command, $"Deleting {remotePath}", cancellationToken);
        return executed.IsSuccess ? SshResult.Success() : SshResult.Failure(executed.Error!);
    }

    public async Task<SshResult<IReadOnlyList<string>>> ListNamesAsync(string remoteDirectory, CancellationToken cancellationToken)
    {
        var command = commands.ListNames(remoteDirectory);
        var executed = await RunElevatedAsync(command, $"Listing {remoteDirectory}", cancellationToken);
        if (!executed.IsSuccess)
        {
            return SshResult<IReadOnlyList<string>>.Failure(executed.Error!);
        }

        var output = executed.Value.StandardOutputText;
        var names = output.Split(NameSeparator, StringSplitOptions.RemoveEmptyEntries);
        return SshResult<IReadOnlyList<string>>.Success(names);
    }

    public ValueTask DisposeAsync()
    {
        return shell.DisposeAsync();
    }

    private async Task<SshResult<string>> StageAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var command = commands.StageUpload();
        var executed = await shell.RunAsUserAsync(command, content, cancellationToken);
        var checkedResult = Check(executed, "Uploading the new content");
        if (!checkedResult.IsSuccess)
        {
            return SshResult<string>.Failure(checkedResult.Error!);
        }

        var stagedPath = checkedResult.Value.StandardOutputText.Trim();
        if (!stagedPath.StartsWith('/'))
        {
            var unexpected = new SshError(SshErrorKind.OperationFailed, "Uploading the new content failed: the temporary file path was not reported.");
            return SshResult<string>.Failure(unexpected);
        }

        return SshResult<string>.Success(stagedPath);
    }

    private async Task<SshResult<RemoteShellCommandResult>> RunElevatedAsync(string command, string action, CancellationToken cancellationToken)
    {
        var executed = await shell.RunElevatedAsync(command, cancellationToken);
        return Check(executed, action);
    }

    private async Task<SshResult<RemoteShellCommandResult>> RunElevatedWithCleanupAsync(string command, string stagedPath, string action, CancellationToken cancellationToken)
    {
        var executed = await shell.RunElevatedWithCleanupAsync(command, stagedPath, cancellationToken);
        return Check(executed, action);
    }

    private static SshResult<RemoteShellCommandResult> Check(SshResult<RemoteShellCommandResult> executed, string action)
    {
        if (!executed.IsSuccess)
        {
            return executed;
        }

        if (executed.Value.Succeeded)
        {
            return executed;
        }

        var failure = ShellCommandFailures.Describe(executed.Value, action);
        return SshResult<RemoteShellCommandResult>.Failure(failure);
    }
}
