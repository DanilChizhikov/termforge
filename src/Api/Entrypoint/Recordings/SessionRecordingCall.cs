using DTech.Core.Terminal.Recording;
using DTech.Core.Terminal.Sessions;

namespace DTech.Entrypoint.Recordings;

internal readonly record struct SessionRecordingCall(string Id, HttpContext Context, TerminalSessionManager SessionManager, TerminalRecordingService RecordingService, TerminalRecordingSettingsService SettingsService);
