namespace DTech.Entrypoint.Docker;

internal sealed record ContainerLogMessage(string Type)
{
    public string? Text { get; init; }

    public int? ExitCode { get; init; }

    public string? Message { get; init; }

    public static ContainerLogMessage Output(string text)
    {
        return new ContainerLogMessage("output") { Text = text };
    }

    public static ContainerLogMessage End(int? exitCode)
    {
        return new ContainerLogMessage("end") { ExitCode = exitCode };
    }

    public static ContainerLogMessage Error(string message)
    {
        return new ContainerLogMessage("error") { Message = message };
    }
}
