namespace DTech.Core.Docker;

public readonly record struct ContainerLogOptions(int Tail, bool Follow)
{
    public const int DefaultTail = 200;
    public const int MaxTail = 5000;

    public static ContainerLogOptions Create(int? requestedTail, bool follow)
    {
        var tail = requestedTail ?? DefaultTail;
        var boundedTail = Math.Clamp(tail, 1, MaxTail);
        return new ContainerLogOptions(boundedTail, follow);
    }
}
