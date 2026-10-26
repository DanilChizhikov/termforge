using DTech.Core.Terminal.Recording;
using Microsoft.AspNetCore.Mvc;

namespace DTech.Entrypoint.Recordings;

internal readonly record struct RecordingSettingsCall([property: FromBody] TerminalRecordingSettingsBody Body, TerminalRecordingSettingsService SettingsService, TerminalRecordingStorage Storage, CancellationToken CancellationToken);
