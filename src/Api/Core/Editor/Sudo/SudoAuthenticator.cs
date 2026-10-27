namespace DTech.Core.Editor.Sudo;

public sealed class SudoAuthenticator(SudoResponseClassifier classifier)
{
    private const string Subject = "sudo";

    public async Task<EditorError?> AuthenticateAsync(SudoShell shell, CancellationToken cancellationToken)
    {
        var executed = await shell.RunElevatedAsync(SudoCommandBuilder.NoOperation, cancellationToken);
        if (!executed.IsSuccess)
        {
            return EditorErrorMapper.FromSsh(executed.Error!, Subject);
        }

        var response = classifier.Classify(executed.Value);
        return SudoErrors.FromResponse(response, executed.Value);
    }
}
