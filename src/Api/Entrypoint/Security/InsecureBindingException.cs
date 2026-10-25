namespace DTech.Entrypoint.Security;

public sealed class InsecureBindingException(string message) : Exception(message);
