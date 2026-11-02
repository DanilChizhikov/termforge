namespace DTech.Credentials.Native.Processes;

public sealed record CredentialHelperRequest(CredentialHelperOperation Operation, string Key, int TimeoutMilliseconds);
