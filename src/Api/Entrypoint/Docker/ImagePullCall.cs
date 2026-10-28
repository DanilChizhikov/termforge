namespace DTech.Entrypoint.Docker;

internal readonly record struct ImagePullCall(Guid Id, string? Image, HttpContext Context);
