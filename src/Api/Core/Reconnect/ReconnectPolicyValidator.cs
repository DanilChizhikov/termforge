namespace DTech.Core.Reconnect;

public static class ReconnectPolicyValidator
{
    public static string? Validate(ReconnectPolicy policy)
    {
        if (!Enum.IsDefined(policy.Mode))
        {
            return "The reconnect mode is not supported.";
        }

        if (policy.MaxAttempts < ReconnectPolicyLimits.MinAttempts || policy.MaxAttempts > ReconnectPolicyLimits.MaxAttempts)
        {
            return $"The reconnect attempt limit must be between {ReconnectPolicyLimits.MinAttempts} and {ReconnectPolicyLimits.MaxAttempts}.";
        }

        var initialOutOfRange = policy.InitialDelaySeconds < ReconnectPolicyLimits.MinDelaySeconds || policy.InitialDelaySeconds > ReconnectPolicyLimits.MaxDelaySeconds;
        var maxOutOfRange = policy.MaxDelaySeconds < ReconnectPolicyLimits.MinDelaySeconds || policy.MaxDelaySeconds > ReconnectPolicyLimits.MaxDelaySeconds;
        if (initialOutOfRange || maxOutOfRange)
        {
            return $"The reconnect delays must be between {ReconnectPolicyLimits.MinDelaySeconds} and {ReconnectPolicyLimits.MaxDelaySeconds} seconds.";
        }

        if (policy.MaxDelaySeconds < policy.InitialDelaySeconds)
        {
            return "The maximum reconnect delay must not be shorter than the initial delay.";
        }

        return null;
    }
}
