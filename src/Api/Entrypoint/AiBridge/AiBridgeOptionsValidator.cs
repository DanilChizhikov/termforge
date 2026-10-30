using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.AiBridge;

internal sealed class AiBridgeOptionsValidator : IValidateOptions<AiBridgeOptions>
{
    private const int MinApprovalTimeoutSeconds = 10;
    private const int MaxApprovalTimeoutSeconds = 900;

    public ValidateOptionsResult Validate(string? name, AiBridgeOptions options)
    {
        if (options.ApprovalTimeoutSeconds < MinApprovalTimeoutSeconds || options.ApprovalTimeoutSeconds > MaxApprovalTimeoutSeconds)
        {
            return ValidateOptionsResult.Fail($"AiBridge:ApprovalTimeoutSeconds must be between {MinApprovalTimeoutSeconds} and {MaxApprovalTimeoutSeconds}");
        }

        return ValidateOptionsResult.Success;
    }
}
