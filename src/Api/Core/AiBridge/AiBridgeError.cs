namespace DTech.Core.AiBridge;

public sealed record AiBridgeError(AiBridgeErrorCode Code, string Message)
{
    public static AiBridgeError BridgeDisabled()
    {
        return new AiBridgeError(AiBridgeErrorCode.BridgeDisabled, "The AI bridge is turned off in TermForge settings.");
    }

    public static AiBridgeError ServerNotFound(Guid connectionId)
    {
        return new AiBridgeError(AiBridgeErrorCode.ServerNotFound, $"Server {connectionId} does not exist. Call list_servers to see the servers you can use.");
    }

    public static AiBridgeError AiAccessDisabled(string serverName)
    {
        return new AiBridgeError(AiBridgeErrorCode.AiAccessDisabled, $"AI access is turned off for {serverName}. The user can turn it on in the server settings.");
    }

    public static AiBridgeError ReadOnlyServer(string serverName)
    {
        return new AiBridgeError(AiBridgeErrorCode.ReadOnlyServer, $"{serverName} is read-only in TermForge. Commands, terminal input and changes are blocked.");
    }

    public static AiBridgeError Invalid(string message)
    {
        return new AiBridgeError(AiBridgeErrorCode.InvalidRequest, message);
    }

    public static AiBridgeError Failed(string message)
    {
        return new AiBridgeError(AiBridgeErrorCode.Failed, message);
    }
}
