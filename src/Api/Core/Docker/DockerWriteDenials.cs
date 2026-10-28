using DTech.Core.Safety;

namespace DTech.Core.Docker;

public static class DockerWriteDenials
{
    public static DockerError ToError(WriteDenial denial, string confirmationMessage)
    {
        return denial == WriteDenial.ReadOnlyServer
            ? DockerError.ReadOnlyServer()
            : new DockerError(DockerErrorCode.ConfirmationRequired, confirmationMessage);
    }
}
