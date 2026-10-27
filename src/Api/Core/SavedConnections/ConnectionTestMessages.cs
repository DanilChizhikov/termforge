namespace DTech.Core.SavedConnections;

public static class ConnectionTestMessages
{
    public static string Describe(ConnectionTestState state, SavedConnectionDraft details)
    {
        var address = $"{details.Host}:{details.Port}";
        return state switch
        {
            ConnectionTestState.AuthenticationFailed => $"Authentication failed for {details.UserName}@{address}. {AuthenticationHint(details.AuthMethod)}",
            ConnectionTestState.Unreachable => $"Could not reach {address}. Check the host name, the port and your network, and that the SSH server is running.",
            ConnectionTestState.Timeout => $"No answer from {address} in time. The host may be offline or a firewall may be dropping packets.",
            ConnectionTestState.HostKeyRejected => $"The host key of {address} was not trusted, so the connection was refused. Host key trust must be established before this server can be used.",
            _ => $"The connection to {address} failed. See the technical detail for the cause.",
        };
    }

    private static string AuthenticationHint(ConnectionAuthMethod method)
    {
        return method switch
        {
            ConnectionAuthMethod.Password => "Check the user name and re-enter the password.",
            ConnectionAuthMethod.KeyFile => "Check the user name and that the key is authorized on the server and has no passphrase.",
            _ => "Check the user name and that the SSH agent is running and holds a key authorized on the server.",
        };
    }
}
