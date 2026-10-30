using System.ComponentModel;
using DTech.Core.AiBridge;
using DTech.Core.AiBridge.Commands;
using ModelContextProtocol.Server;

namespace DTech.Entrypoint.AiBridge.Tools;

[McpServerToolType]
public sealed class AiServerTools(AiServerAccessPolicy accessPolicy, AiCommandService commandService)
{
    [McpServerTool(Name = "list_servers", ReadOnly = true)]
    [Description("Lists the TermForge servers the user opened for AI access. Use the id in other tools.")]
    public async Task<IReadOnlyList<AiServerResponse>> ListServers(CancellationToken cancellationToken)
    {
        var listed = await accessPolicy.ListAccessibleAsync(cancellationToken);
        var connections = AiToolResults.ValueOrThrow(listed);
        var servers = new List<AiServerResponse>(connections.Count);
        for (var i = 0; i < connections.Count; i++)
        {
            var server = AiServerResponse.From(connections[i]);
            servers.Add(server);
        }

        return servers;
    }

    [McpServerTool(Name = "run_command", Destructive = true)]
    [Description("Runs a shell command on a server over SSH without a terminal and returns stdout, stderr and the exit code. The user may have to approve it in TermForge first.")]
    public async Task<AiCommandResponse> RunCommand(
        [Description("Server id from list_servers.")] Guid serverId,
        [Description("Shell command to run.")] string command,
        [Description("Timeout in seconds, 1 to 300. Defaults to 60.")] int? timeoutSeconds = null,
        CancellationToken cancellationToken = default)
    {
        var request = new AiCommandRequest(serverId, command, timeoutSeconds);
        var executed = await commandService.RunAsync(request, cancellationToken);
        var result = AiToolResults.ValueOrThrow(executed);
        return AiCommandResponse.From(result);
    }
}
