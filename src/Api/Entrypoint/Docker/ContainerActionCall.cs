using Microsoft.AspNetCore.Mvc;

namespace DTech.Entrypoint.Docker;

internal readonly record struct ContainerActionCall(Guid Id, string Container, string Action, [property: FromBody] ContainerActionBody? Body);
