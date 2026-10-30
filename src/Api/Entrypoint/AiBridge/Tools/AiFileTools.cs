using System.ComponentModel;
using DTech.Core.AiBridge.Files;
using ModelContextProtocol.Server;

namespace DTech.Entrypoint.AiBridge.Tools;

[McpServerToolType]
public sealed class AiFileTools(AiFileReader reader, AiFileWriter writer)
{
    [McpServerTool(Name = "read_file", ReadOnly = true)]
    [Description("Reads a UTF-8 text file on a server over SFTP and returns its content and sha256. Pass the sha256 to write_file.")]
    public async Task<AiFileResponse> ReadFile(
        [Description("Server id from list_servers.")] Guid serverId,
        [Description("Absolute path of the file.")] string path,
        CancellationToken cancellationToken)
    {
        var read = await reader.ReadAsync(serverId, path, cancellationToken);
        var document = AiToolResults.ValueOrThrow(read);
        return AiFileResponse.From(document);
    }

    [McpServerTool(Name = "write_file", Destructive = true)]
    [Description("Replaces a text file on a server with new content, keeping a backup, owner and permissions. Pass the sha256 from read_file; omit it only to create a new file. The user may have to approve the diff first.")]
    public async Task<AiFileWriteResult> WriteFile(
        [Description("Server id from list_servers.")] Guid serverId,
        [Description("Absolute path of the file.")] string path,
        [Description("The complete new content of the file.")] string content,
        [Description("sha256 returned by read_file. Omit to create a new file.")] string? expectedSha256 = null,
        CancellationToken cancellationToken = default)
    {
        var write = new AiFileWrite(serverId, path, content, expectedSha256);
        var written = await writer.WriteAsync(write, cancellationToken);
        return AiToolResults.ValueOrThrow(written);
    }
}
