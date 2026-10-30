using DTech.Core.Diagnostics;
using DTech.Core.Docker;
using DTech.Core.Editor;
using DTech.Core.SavedConnections;
using DTech.Core.Terminal.Sessions;

namespace DTech.Entrypoint.AiBridge.Tools;

public sealed record AiServerResponse(Guid Id, string Name, string Host, int Port, string User, string? Group, bool ReadOnly)
{
    public static AiServerResponse From(SavedConnection connection)
    {
        var details = connection.Details;
        return new AiServerResponse(connection.Id, details.Name, details.Host, details.Port, details.UserName, details.GroupName, details.ReadOnly);
    }
}

public sealed record AiCommandResponse(int ExitCode, string Stdout, string Stderr, bool OutputTruncated)
{
    public static AiCommandResponse From(RemoteCommandResult result)
    {
        return new AiCommandResponse(result.ExitCode, result.StandardOutput, result.StandardError, result.OutputTruncated);
    }
}

public sealed record AiTerminalResponse(string Id, Guid ServerId, string ServerName, int Number, DateTime OpenedAt)
{
    public static AiTerminalResponse From(TerminalSessionDescriptor descriptor)
    {
        return new AiTerminalResponse(descriptor.Id.Value, descriptor.ConnectionId, descriptor.ConnectionName, descriptor.SessionNumber, descriptor.OpenedAt);
    }
}

public sealed record AiTerminalOutputResponse(string TerminalId, string ServerName, string Text, bool Truncated);

public sealed record AiContainerResponse(string Id, string Name, string Image, string State, string Status, string? Health, string Ports, string? ComposeProject)
{
    public static AiContainerResponse From(DockerContainer container)
    {
        var state = container.State.ToString().ToLowerInvariant();
        return new AiContainerResponse(container.Id, container.Name, container.Image, state, container.Status, container.Health, container.Ports, container.Compose?.Project);
    }
}

public sealed record AiContainersResponse(string ServerName, bool DockerAvailable, string? Reason, IReadOnlyList<AiContainerResponse> Containers);

public sealed record AiImagesResponse(string ServerName, IReadOnlyList<DockerImage> Images);

public sealed record AiContainerActionResponse(string Container, string Action, Guid OperationId);

public sealed record AiFileResponse(string Path, string Content, string Sha256, long Size, string Mode)
{
    public static AiFileResponse From(RemoteDocument document)
    {
        var mode = Convert.ToString(document.Mode, 8);
        return new AiFileResponse(document.Path, document.Content.Text, document.Version.Sha256, document.Version.Size, mode);
    }
}
