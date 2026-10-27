using Microsoft.AspNetCore.Mvc;

namespace DTech.Entrypoint.Docker;

internal readonly record struct ContainerShellCall(Guid Id, string Container, [property: FromBody] OpenContainerShellBody Body, HttpContext Context);
