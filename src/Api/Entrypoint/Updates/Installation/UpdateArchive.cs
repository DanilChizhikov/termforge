using System.Formats.Tar;
using System.IO.Compression;

namespace DTech.Entrypoint.Updates.Installation;

internal static class UpdateArchive
{
    private const long MaximumExpandedSize = 4L * 1024 * 1024 * 1024;

    public static async Task ExtractAsync(Extraction request, CancellationToken cancellationToken)
    {
        UpdateFiles.CreatePrivateDirectory(request.Destination);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long expanded = 0;
        if (request.Windows)
        {
            using var zip = ZipFile.OpenRead(request.Archive);
            for (var i = 0; i < zip.Entries.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = zip.Entries[i];
                if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                    throw new InvalidDataException("Archive links are not supported.");
                var target = Destination(request, entry.FullName);
                if (entry.FullName.EndsWith('/')) { Directory.CreateDirectory(target); continue; }
                if (!paths.Add(target)) throw new InvalidDataException("Duplicate archive entry.");
                expanded = checked(expanded + entry.Length);
                if (expanded > MaximumExpandedSize) throw new InvalidDataException("The expanded archive is too large.");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using var input = entry.Open();
                await using var output = File.Create(target);
                await input.CopyToAsync(output, cancellationToken);
            }
        }
        else
        {
            await using var file = File.OpenRead(request.Archive);
            await using var gzip = new GZipStream(file, CompressionMode.Decompress);
            using var tar = new TarReader(gzip);
            TarEntry? entry;
            while ((entry = await tar.GetNextEntryAsync(cancellationToken: cancellationToken)) is not null)
            {
                if (entry.EntryType is not (TarEntryType.Directory or TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                    throw new InvalidDataException("Only regular files and directories are accepted in update archives.");
                var target = Destination(request, entry.Name);
                if (entry.EntryType == TarEntryType.Directory) { Directory.CreateDirectory(target); continue; }
                if (!paths.Add(target)) throw new InvalidDataException("Duplicate archive entry.");
                expanded = checked(expanded + entry.Length);
                if (expanded > MaximumExpandedSize) throw new InvalidDataException("The expanded archive is too large.");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using var output = File.Create(target);
                if (entry.DataStream is not null) await entry.DataStream.CopyToAsync(output, cancellationToken);
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(target, entry.Mode & (UnixFileMode)0x1FF);
            }
        }
        var executable = Path.Combine(request.Destination, request.Windows ? "TermForge.exe" : "TermForge");
        if (!File.Exists(executable) || !File.Exists(Path.Combine(request.Destination, "wwwroot", "index.html")) || !File.Exists(Path.Combine(request.Destination, "appsettings.json")))
            throw new InvalidDataException("The archive does not contain a complete TermForge installation.");
    }

    private static string Destination(Extraction request, string name)
    {
        if (name == request.Prefix.TrimEnd('/')) return request.Destination;
        if (!name.StartsWith(request.Prefix, StringComparison.Ordinal) || name.Contains('\\') || name.Contains(':'))
            throw new InvalidDataException("Unexpected archive root or path.");
        var relative = name[request.Prefix.Length..].TrimEnd('/');
        var segments = relative.Split('/');
        if (segments.Any(segment => segment is ".." or ".") || relative.StartsWith('/'))
            throw new InvalidDataException("Archive paths must stay inside staging.");
        if (relative.Length == 0) return request.Destination;
        if (segments.Any(segment => segment.Length == 0 || segment.EndsWith(' ') || segment.EndsWith('.')
            || segment.Any(character => char.IsControl(character) || "<>:\"\\|?*".Contains(character))))
            throw new InvalidDataException("Archive paths must use unambiguous portable file names.");
        var first = segments[0];
        if (first != "wwwroot" && first != "TermForge" && first != "TermForge.exe" && first != "appsettings.json" && first != "appsettings.Development.json"
            && first != "TermForge.staticwebassets.endpoints.json" && first != "web.config")
            throw new InvalidDataException("The archive contains unexpected application files.");
        return Path.Combine(request.Destination, relative);
    }

    internal sealed record Extraction(string Archive, string Destination, string Prefix, bool Windows);
}
