using DTech.Core.Diagnostics.Parsing;

namespace DTech.Core.Diagnostics.Probes;

public sealed class SudoProbe : ICapabilityProbe
{
    private const string Name = "Passwordless sudo";

    public async Task<IReadOnlyList<CapabilityResult>> ProbeAsync(CapabilityProbeContext context, CancellationToken cancellationToken)
    {
        var presence = await ToolLookup.FindAsync(context.Runner, "sudo", cancellationToken);
        if (presence == ToolPresence.Unknown)
        {
            return [CapabilityResult.Unknown(CapabilityIds.Sudo, Name, "Could not check whether sudo is installed.")];
        }

        if (presence == ToolPresence.Missing)
        {
            return [CapabilityResult.Unavailable(CapabilityIds.Sudo, Name, "sudo is not installed on the server.")];
        }

        var result = await context.Runner.RunScriptAsync(SudoOutputParser.Script, cancellationToken);
        if (!result.IsSuccess)
        {
            var failure = ToolLookup.DescribeFailure("run sudo", result.Error?.Message);
            return [CapabilityResult.Unknown(CapabilityIds.Sudo, Name, failure)];
        }

        var access = SudoOutputParser.Parse(result.Value);
        return [Describe(access)];
    }

    private static CapabilityResult Describe(SudoAccess access)
    {
        return access switch
        {
            SudoAccess.Passwordless => CapabilityResult.Available(CapabilityIds.Sudo, Name, "The user can run sudo without a password."),
            SudoAccess.PasswordRequired => CapabilityResult.Unavailable(CapabilityIds.Sudo, Name, "sudo asks for a password. Grant NOPASSWD in sudoers for this user to run privileged actions."),
            SudoAccess.NotPermitted => CapabilityResult.Unavailable(CapabilityIds.Sudo, Name, "The user is not allowed to use sudo. Add the user to the sudoers configuration."),
            _ => CapabilityResult.Unknown(CapabilityIds.Sudo, Name, "sudo returned an unrecognized answer."),
        };
    }
}
