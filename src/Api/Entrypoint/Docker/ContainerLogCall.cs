namespace DTech.Entrypoint.Docker;

internal readonly record struct ContainerLogCall(Guid Id, string Container, int? Tail, bool? Follow, HttpContext Context);
