using DTech.Core.Safety;

namespace DTech.Core.Editor;

public sealed record EditorError(EditorErrorCode Code, string Message)
{
    public static EditorError FromDenial(WriteDenial denial, string? path)
    {
        return denial == WriteDenial.ReadOnlyServer
            ? new EditorError(EditorErrorCode.ReadOnlyServer, ServerWriteGate.ReadOnlyMessage)
            : new EditorError(EditorErrorCode.ConfirmationRequired, $"Rolling back {path} requires explicit confirmation.");
    }

    public static EditorError Invalid(string message)
    {
        return new EditorError(EditorErrorCode.Invalid, message);
    }

    public static EditorError NotFound(string path)
    {
        return new EditorError(EditorErrorCode.NotFound, $"File does not exist: {path}");
    }

    public static EditorError NotAFile(string path)
    {
        return new EditorError(EditorErrorCode.NotAFile, $"Only regular files can be edited: {path}");
    }

    public static EditorError SymbolicLink(string path)
    {
        return new EditorError(EditorErrorCode.NotAFile, $"{path} is a symbolic link. Open the file it points to instead.");
    }

    public static EditorError TooLarge(string path, long maxBytes)
    {
        return new EditorError(EditorErrorCode.TooLarge, $"{path} is larger than the editor limit of {maxBytes} bytes.");
    }

    public static EditorError Binary(string path)
    {
        return new EditorError(EditorErrorCode.Binary, $"{path} looks like a binary file and cannot be edited as text.");
    }

    public static EditorError NotUtf8(string path)
    {
        return new EditorError(EditorErrorCode.Binary, $"{path} is not valid UTF-8 text and cannot be edited.");
    }

    public EditorError Prefixed(string prefix)
    {
        return this with { Message = $"{prefix} {Message}" };
    }
}
