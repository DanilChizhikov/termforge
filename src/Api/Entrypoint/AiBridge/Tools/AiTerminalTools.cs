using System.ComponentModel;
using DTech.Core.AiBridge.Terminals;
using ModelContextProtocol.Server;

namespace DTech.Entrypoint.AiBridge.Tools;

[McpServerToolType]
public sealed class AiTerminalTools(AiTerminalLocator locator, AiTerminalOutputReader outputReader, AiTerminalInputSender inputSender)
{
    [McpServerTool(Name = "list_terminals", ReadOnly = true)]
    [Description("Lists the terminals the user has open in TermForge on servers with AI access.")]
    public async Task<IReadOnlyList<AiTerminalResponse>> ListTerminals(CancellationToken cancellationToken)
    {
        var listed = await locator.ListAsync(cancellationToken);
        var descriptors = AiToolResults.ValueOrThrow(listed);
        var terminals = new List<AiTerminalResponse>(descriptors.Count);
        for (var i = 0; i < descriptors.Count; i++)
        {
            var terminal = AiTerminalResponse.From(descriptors[i]);
            terminals.Add(terminal);
        }

        return terminals;
    }

    [McpServerTool(Name = "read_terminal_output", ReadOnly = true)]
    [Description("Returns the latest output of an open terminal as plain text without escape sequences.")]
    public async Task<AiTerminalOutputResponse> ReadTerminalOutput(
        [Description("Terminal id from list_terminals.")] string terminalId,
        [Description("Maximum number of characters from the end of the output. Defaults to 8000.")] int? maxCharacters = null,
        CancellationToken cancellationToken = default)
    {
        var request = new AiTerminalOutputRequest(terminalId, maxCharacters);
        var read = await outputReader.ReadAsync(request, cancellationToken);
        var output = AiToolResults.ValueOrThrow(read);
        return new AiTerminalOutputResponse(output.Terminal.Id.Value, output.Terminal.ConnectionName, output.Text, output.Truncated);
    }

    [McpServerTool(Name = "send_to_terminal", Destructive = true)]
    [Description("Types text into an open terminal that the user sees in TermForge, optionally followed by Enter. The user may have to approve it first. Use read_terminal_output to see the result.")]
    public async Task<AiTerminalResponse> SendToTerminal(
        [Description("Terminal id from list_terminals.")] string terminalId,
        [Description("Text to type.")] string text,
        [Description("Press Enter after the text. Defaults to true.")] bool pressEnter = true,
        CancellationToken cancellationToken = default)
    {
        var input = new AiTerminalInput(terminalId, text, pressEnter);
        var sent = await inputSender.SendAsync(input, cancellationToken);
        var descriptor = AiToolResults.ValueOrThrow(sent);
        return AiTerminalResponse.From(descriptor);
    }
}
