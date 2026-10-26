namespace DTech.Core.Diagnostics.Probes;

public static class ToolLookup
{
    public static async Task<ToolPresence> FindAsync(IRemoteCommandRunner runner, string tool, CancellationToken cancellationToken)
    {
        var script = $"command -v {tool} >/dev/null 2>&1";
        var result = await runner.RunScriptAsync(script, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToolPresence.Unknown;
        }

        return result.Value.Succeeded ? ToolPresence.Present : ToolPresence.Missing;
    }

    public static string DescribeFailure(string action, string? errorMessage)
    {
        var detail = RemoteText.Sanitize(errorMessage, 200);
        return $"Could not {action}: {detail}";
    }
}
