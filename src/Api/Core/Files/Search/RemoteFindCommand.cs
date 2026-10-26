using DTech.Core.Diagnostics;

namespace DTech.Core.Files.Search;

public static class RemoteFindCommand
{
    public const int TimeLimitExitCode = 143;

    public static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(10);

    private const int BytesPerResult = 1024;
    private const int MinOutputBytes = 64 * 1024;
    private const int MaxOutputBytes = 8 * 1024 * 1024;

    public static string Build(FileSearchQuery query)
    {
        var script = BuildScript(query);
        return ShellScript.Wrap(script);
    }

    public static string BuildScript(FileSearchQuery query)
    {
        var root = ShellArgument.Quote(query.RootPath);
        var glob = ShellArgument.Quote(query.Pattern.ToFindGlob());
        var maxDepth = query.Limits.MaxDepth;
        var seconds = (int)Math.Ceiling(query.Limits.TimeLimit.TotalSeconds);
        var find = $"find {root} -mindepth 1 -maxdepth {maxDepth} -iname {glob} \\( -type d -exec printf 'd%s\\0' {{}} + -o -exec printf 'f%s\\0' {{}} + \\)";
        return $"{find} & search=$!; ( sleep {seconds}; kill $search ) </dev/null >/dev/null 2>&1 & timer=$!; wait $search; status=$?; kill $timer 2>/dev/null; exit $status";
    }

    public static RemoteCommandRequest CreateRequest(FileSearchQuery query)
    {
        var command = Build(query);
        var timeout = query.Limits.TimeLimit + GracePeriod;
        var outputBytes = Math.Clamp(query.Limits.MaxResults * BytesPerResult, MinOutputBytes, MaxOutputBytes);
        return new RemoteCommandRequest(command, timeout, outputBytes);
    }
}
