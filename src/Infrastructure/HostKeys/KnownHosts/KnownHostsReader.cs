using Microsoft.Extensions.Logging;

namespace DTech.HostKeys.KnownHosts;

internal sealed class KnownHostsReader(KnownHostsFiles files, ILogger<KnownHostsReader> logger)
{
    private const char CommentPrefix = '#';

    public IReadOnlyList<KnownHostsEntry> ReadEntries()
    {
        var entries = new List<KnownHostsEntry>();
        var paths = files.Resolve();
        for (var i = 0; i < paths.Count; i++)
        {
            var path = paths[i];
            ReadFile(path, entries);
        }

        return entries;
    }

    private void ReadFile(string path, List<KnownHostsEntry> entries)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var skippedLines = 0;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            var entry = KnownHostsLineParser.Parse(line);
            if (entry is not null)
            {
                entries.Add(entry);
                continue;
            }

            if (IsContentLine(line))
            {
                skippedLines++;
            }
        }

        if (skippedLines > 0)
        {
            logger.LogDebug($"Skipped {skippedLines} unsupported or malformed lines in {path}");
        }
    }

    private static bool IsContentLine(string line)
    {
        var trimmedLine = line.Trim();
        return trimmedLine.Length > 0 && trimmedLine[0] != CommentPrefix;
    }
}
