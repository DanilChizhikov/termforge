namespace DTech.Entrypoint.Terminals;

internal sealed record TerminalServerMessage(string Type)
{
    public const string StatusType = "status";
    public const string ErrorType = "error";
    public const string ConnectedState = "connected";
    public const string DisconnectedState = "disconnected";

    public string? State { get; init; }

    public string? Reason { get; init; }

    public string? Message { get; init; }

    public static TerminalServerMessage Connected()
    {
        return new TerminalServerMessage(StatusType) { State = ConnectedState };
    }

    public static TerminalServerMessage Disconnected(string reason)
    {
        return new TerminalServerMessage(StatusType) { State = DisconnectedState, Reason = reason };
    }

    public static TerminalServerMessage Error(string message)
    {
        return new TerminalServerMessage(ErrorType) { Message = message };
    }
}
