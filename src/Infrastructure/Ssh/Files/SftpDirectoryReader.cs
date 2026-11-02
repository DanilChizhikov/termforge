using DTech.Core.Files;
using Renci.SshNet;

namespace DTech.Ssh.Files;

internal sealed class SftpDirectoryReader(ISftpClient client)
{
    private const int MaxConcurrentLinkResolutions = 8;

    private readonly SftpEntryMapper _mapper = new(client);

    public async Task<IReadOnlyList<RemoteFileEntry>> ListAsync(string remotePath, bool resolveLinkTargets, CancellationToken cancellationToken)
    {
        var entries = new List<RemoteFileEntry>();
        await foreach (var file in client.ListDirectoryAsync(remotePath, cancellationToken))
        {
            if (file.Name is "." or "..")
            {
                continue;
            }

            var entry = SftpEntryMapper.Map(file);
            entries.Add(entry);
        }

        if (!resolveLinkTargets)
        {
            return entries;
        }

        var resolved = entries.ToArray();
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = MaxConcurrentLinkResolutions,
            CancellationToken = cancellationToken,
        };
        await Parallel.ForAsync(0, resolved.Length, options, async (index, token) =>
        {
            resolved[index] = await _mapper.DescribeLinkAsync(resolved[index], token);
        });
        return resolved;
    }
}
