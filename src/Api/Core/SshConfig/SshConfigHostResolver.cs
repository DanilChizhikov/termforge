namespace DTech.Core.SshConfig;

public sealed class SshConfigHostResolver(SshConfigEnvironment environment, SshConfigIssueCollector issues)
{
    public const int DefaultPort = 22;

    public SshConfigHostEntry Resolve(string alias, IReadOnlyList<SshConfigBlock> blocks)
    {
        var settings = new ResolvedSettings();
        for (var blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
        {
            var block = blocks[blockIndex];
            if (!block.AppliesTo(alias))
            {
                continue;
            }

            for (var directiveIndex = 0; directiveIndex < block.Directives.Count; directiveIndex++)
            {
                Apply(block.Directives[directiveIndex], alias, settings);
            }
        }

        return new SshConfigHostEntry(alias, settings.HostName ?? alias, settings.Port ?? DefaultPort, settings.UserName ?? environment.UserName)
        {
            IdentityFile = settings.IdentityFile,
            ProxyJump = settings.ProxyJump,
        };
    }

    private void Apply(SshConfigDirective directive, string alias, ResolvedSettings settings)
    {
        var keyword = directive.Keyword.ToLowerInvariant();
        switch (keyword)
        {
            case "hostname":
                settings.HostName ??= ReadSingleValue(directive, alias);
                break;
            case "user":
                settings.UserName ??= ReadSingleValue(directive, alias);
                break;
            case "port":
                settings.Port ??= ReadPort(directive, alias);
                break;
            case "proxyjump":
                ApplyProxyJump(directive, alias, settings);
                break;
            case "identityfile":
                ApplyIdentityFile(directive, alias, settings);
                break;
            default:
                issues.Add(directive, "This directive is not supported by the SSH transport and was ignored.", alias);
                break;
        }
    }

    private void ApplyProxyJump(SshConfigDirective directive, string alias, ResolvedSettings settings)
    {
        if (settings.ProxyJumpDecided)
        {
            return;
        }

        var value = ReadSingleValue(directive, alias);
        if (value is null)
        {
            return;
        }

        settings.ProxyJumpDecided = true;
        settings.ProxyJump = string.Equals(value, "none", StringComparison.OrdinalIgnoreCase) ? null : value;
    }

    private void ApplyIdentityFile(SshConfigDirective directive, string alias, ResolvedSettings settings)
    {
        var value = ReadSingleValue(directive, alias);
        if (value is null || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (settings.IdentityFile is not null)
        {
            issues.Add(directive, "Only the first IdentityFile of a host is used; this one was ignored.", alias);
            return;
        }

        var expanded = SshConfigPathExpander.Expand(value, environment.HomeDirectory);
        settings.IdentityFile = Path.IsPathRooted(expanded) ? expanded : Path.Combine(environment.HomeDirectory, expanded);
    }

    private int? ReadPort(SshConfigDirective directive, string alias)
    {
        var value = ReadSingleValue(directive, alias);
        if (value is null)
        {
            return null;
        }

        if (int.TryParse(value, out var port) && port is >= 1 and <= 65535)
        {
            return port;
        }

        issues.Add(directive, $"'{value}' is not a valid port.", alias);
        return null;
    }

    private string? ReadSingleValue(SshConfigDirective directive, string alias)
    {
        if (directive.Values.Count != 1)
        {
            issues.Add(directive, "Expected exactly one value; the directive was ignored.", alias);
            return null;
        }

        var value = directive.Values[0];
        if (value.Contains('%'))
        {
            issues.Add(directive, "Percent tokens such as %h or %r are not supported; the directive was ignored.", alias);
            return null;
        }

        return value;
    }

    private sealed class ResolvedSettings
    {
        public string? HostName { get; set; }

        public string? UserName { get; set; }

        public int? Port { get; set; }

        public string? ProxyJump { get; set; }

        public bool ProxyJumpDecided { get; set; }

        public string? IdentityFile { get; set; }
    }
}
