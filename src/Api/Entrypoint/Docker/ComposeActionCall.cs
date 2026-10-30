using Microsoft.AspNetCore.Mvc;

namespace DTech.Entrypoint.Docker;

internal readonly record struct ComposeActionCall(Guid Id, string Project, string Action, [property: FromBody] ContainerActionBody? Body);
