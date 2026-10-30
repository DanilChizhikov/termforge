using System.Text;
using DTech.Core.Terminal;

namespace DTech.Core.AiBridge.Terminals;

public sealed class AiTerminalOutputReader(AiTerminalLocator locator)
{
    public async Task<AiBridgeResult<AiTerminalOutput>> ReadAsync(AiTerminalOutputRequest request, CancellationToken cancellationToken)
    {
        var found = await locator.FindAsync(request.TerminalId, false, cancellationToken);
        if (!found.IsSuccess)
        {
            return AiBridgeResult<AiTerminalOutput>.Failure(found.Error);
        }

        var session = found.Value.Session;
        var bytes = session.OutputTail.Snapshot();
        var decoded = Encoding.UTF8.GetString(bytes);
        var plainText = AnsiText.Strip(decoded);
        var maxCharacters = request.BoundedMaxCharacters;
        var truncated = plainText.Length > maxCharacters;
        var text = truncated ? plainText[^maxCharacters..] : plainText;
        var output = new AiTerminalOutput(session.Descriptor, text, truncated);
        return AiBridgeResult<AiTerminalOutput>.Success(output);
    }
}
