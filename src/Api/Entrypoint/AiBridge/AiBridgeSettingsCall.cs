using DTech.Core.AiBridge.Settings;
using Microsoft.AspNetCore.Mvc;

namespace DTech.Entrypoint.AiBridge;

internal sealed record AiBridgeSettingsCall(
    [property: FromServices] AiBridgeSettingsService Settings,
    [property: FromServices] AiBridgeTokenService Tokens);
