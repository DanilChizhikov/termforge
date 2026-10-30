using System.Globalization;
using System.Text;
using DTech.Core.AiBridge.Approvals;
using DTech.Core.Terminal.Sessions;

namespace DTech.Core.AiBridge.Terminals;

public sealed class AiTerminalInputSender(AiTerminalLocator locator, AiActionGate actionGate, AiOperationRecorder recorder)
{
    public const int MaxInputLength = 8000;

    private const string EnterKey = "\r";

    public async Task<AiBridgeResult<TerminalSessionDescriptor>> SendAsync(AiTerminalInput input, CancellationToken cancellationToken)
    {
        var invalid = Validate(input);
        if (invalid is not null)
        {
            return AiBridgeResult<TerminalSessionDescriptor>.Failure(invalid);
        }

        var found = await locator.FindAsync(input.TerminalId, true, cancellationToken);
        if (!found.IsSuccess)
        {
            return AiBridgeResult<TerminalSessionDescriptor>.Failure(found.Error);
        }

        var session = found.Value.Session;
        var serverName = found.Value.Connection.Details.Name;
        var text = input.Text ?? string.Empty;
        var summary = input.PressEnter ? $"{text} (Enter)" : text;
        var action = new AiActionDescription(found.Value.Connection.Id, serverName, AiActionKind.TerminalInput, summary);
        var rejection = await actionGate.AuthorizeAsync(action, cancellationToken);
        if (rejection is not null)
        {
            return AiBridgeResult<TerminalSessionDescriptor>.Failure(rejection);
        }

        var sessionNumber = session.Descriptor.SessionNumber.ToString(CultureInfo.InvariantCulture);
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["command"] = summary,
            ["session"] = sessionNumber,
        };
        var entry = new AiOperationEntry(AiOperationRecorder.TerminalInputKind, serverName, details);
        var operationId = await recorder.StartAsync(entry, cancellationToken);
        var payload = input.PressEnter ? text + EnterKey : text;
        var bytes = Encoding.UTF8.GetBytes(payload);
        var written = await session.WriteAsync(bytes, cancellationToken);
        if (!written.IsSuccess)
        {
            await recorder.FailAsync(operationId, written.Error!.Message);
            var failure = AiBridgeError.Failed(written.Error.Message);
            return AiBridgeResult<TerminalSessionDescriptor>.Failure(failure);
        }

        await recorder.SucceedAsync(operationId, null);
        return AiBridgeResult<TerminalSessionDescriptor>.Success(session.Descriptor);
    }

    private static AiBridgeError? Validate(AiTerminalInput input)
    {
        var text = input.Text ?? string.Empty;
        if (text.Length == 0 && !input.PressEnter)
        {
            return AiBridgeError.Invalid("Enter text to send or set pressEnter.");
        }

        return text.Length > MaxInputLength ? AiBridgeError.Invalid($"The input is longer than {MaxInputLength} characters.") : null;
    }
}
