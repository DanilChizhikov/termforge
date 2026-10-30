using DTech.Core.Docker;
using DTech.Core.Editor;
using DTech.Core.Files;

namespace DTech.Core.AiBridge;

public static class AiBridgeErrorMapper
{
    public static AiBridgeError FromDocker(DockerError error)
    {
        var code = error.Code switch
        {
            DockerErrorCode.InvalidRequest => AiBridgeErrorCode.InvalidRequest,
            DockerErrorCode.ReadOnlyServer => AiBridgeErrorCode.ReadOnlyServer,
            DockerErrorCode.ContainerNotFound or DockerErrorCode.ImageNotFound or DockerErrorCode.ConnectionNotFound => AiBridgeErrorCode.NotFound,
            _ => AiBridgeErrorCode.Failed,
        };
        return new AiBridgeError(code, error.Message);
    }

    public static AiBridgeError FromEditor(EditorError error)
    {
        var code = error.Code switch
        {
            EditorErrorCode.Invalid or EditorErrorCode.NotAFile or EditorErrorCode.TooLarge or EditorErrorCode.Binary => AiBridgeErrorCode.InvalidRequest,
            EditorErrorCode.NotFound => AiBridgeErrorCode.NotFound,
            EditorErrorCode.ReadOnlyServer => AiBridgeErrorCode.ReadOnlyServer,
            _ => AiBridgeErrorCode.Failed,
        };
        return new AiBridgeError(code, error.Message);
    }

    public static AiBridgeError FromFile(FileError error)
    {
        return AiBridgeError.Failed(error.Message);
    }
}
