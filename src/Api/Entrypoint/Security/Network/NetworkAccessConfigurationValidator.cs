namespace DTech.Entrypoint.Security.Network;

internal static class NetworkAccessConfigurationValidator
{
    public static IReadOnlyList<string> FindProblems(NetworkAccessOptions options, IReadOnlyList<NonLoopbackBinding> bindings)
    {
        var problems = new List<string>();
        AddPasswordHashProblem(options, problems);
        AddAllowedHostProblems(options, problems);
        AddLimitProblems(options, problems);
        AddInsecureBindingProblems(options, bindings, problems);
        return problems;
    }

    private static void AddPasswordHashProblem(NetworkAccessOptions options, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(options.PasswordHash))
        {
            problems.Add("NetworkAccess:PasswordHash is not configured; generate it with `TermForge --hash-password`");
            return;
        }

        if (!PasswordHash.TryParse(options.PasswordHash, out _))
        {
            problems.Add($"NetworkAccess:PasswordHash is not a valid {PasswordHash.AlgorithmName} hash with at least {PasswordHash.MinimumIterations} iterations; generate it with `TermForge --hash-password`");
        }
    }

    private static void AddAllowedHostProblems(NetworkAccessOptions options, List<string> problems)
    {
        var allowedHosts = options.AllowedHosts;
        if (allowedHosts.Count == 0)
        {
            problems.Add("NetworkAccess:AllowedHosts must list the host names or IP addresses used to reach TermForge from the network");
            return;
        }

        for (var i = 0; i < allowedHosts.Count; i++)
        {
            var allowedHost = allowedHosts[i];
            if (!NetworkHostName.IsValid(allowedHost))
            {
                problems.Add($"NetworkAccess:AllowedHosts entry '{allowedHost}' is not a host name or IP address (no scheme, port or wildcard)");
            }
        }
    }

    private static void AddLimitProblems(NetworkAccessOptions options, List<string> problems)
    {
        AddPositiveProblem("NetworkAccess:MaxFailedLoginAttempts", options.MaxFailedLoginAttempts, problems);
        AddPositiveProblem("NetworkAccess:GlobalMaxFailedLoginAttempts", options.GlobalMaxFailedLoginAttempts, problems);
        AddPositiveProblem("NetworkAccess:LockoutMinutes", options.LockoutMinutes, problems);
        AddPositiveProblem("NetworkAccess:SessionLifetimeHours", options.SessionLifetimeHours, problems);
    }

    private static void AddPositiveProblem(string key, int value, List<string> problems)
    {
        if (value <= 0)
        {
            problems.Add($"{key} must be greater than zero");
        }
    }

    private static void AddInsecureBindingProblems(NetworkAccessOptions options, IReadOnlyList<NonLoopbackBinding> bindings, List<string> problems)
    {
        if (options.AllowInsecureHttp)
        {
            return;
        }

        for (var i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            if (!binding.IsHttps)
            {
                problems.Add($"{binding.Description} and uses plain HTTP; configure an HTTPS endpoint or set NetworkAccess:AllowInsecureHttp=true to accept the risk");
            }
        }
    }
}
