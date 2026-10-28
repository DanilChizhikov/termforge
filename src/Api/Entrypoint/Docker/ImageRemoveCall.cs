using Microsoft.AspNetCore.Mvc;

namespace DTech.Entrypoint.Docker;

internal readonly record struct ImageRemoveCall(Guid Id, [property: FromBody] ImageRemoveBody Body);
