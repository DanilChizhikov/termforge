using DTech.Core.Connections;

namespace DTech.Core.Editor.Sudo;

public sealed class SudoAvailabilityProbe(SudoCommandBuilder commands, SudoResponseClassifier classifier)
{
    private const string PasswordlessMessage = "sudo works without a password for this user.";
    private const int MaxOutputBytes = 16 * 1024;

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(20);

    public async Task<SshResult<SudoProbeOutcome>> ProbeAsync(IRemoteShellCommandRunner runner, CancellationToken cancellationToken)
    {
        var text = commands.Probe();
        var command = new RemoteShellCommand(text, ProbeTimeout, MaxOutputBytes);
        var executed = await runner.RunAsync(command, cancellationToken);
        if (!executed.IsSuccess)
        {
            return SshResult<SudoProbeOutcome>.Failure(executed.Error!);
        }

        var response = classifier.Classify(executed.Value);
        var outcome = Describe(response, executed.Value);
        return SshResult<SudoProbeOutcome>.Success(outcome);
    }

    private static SudoProbeOutcome Describe(SudoResponse response, RemoteShellCommandResult result)
    {
        if (response == SudoResponse.Granted)
        {
            return new SudoProbeOutcome(SudoAvailability.Passwordless, PasswordlessMessage);
        }

        var error = SudoErrors.FromResponse(response, result)!;
        var availability = response is SudoResponse.PasswordRequired or SudoResponse.PasswordIncorrect
            ? SudoAvailability.PasswordRequired
            : SudoAvailability.Unavailable;
        return new SudoProbeOutcome(availability, error.Message);
    }
}
