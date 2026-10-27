using DTech.Core.Terminal.Recording;

namespace DTech.Core.SettingsTransfer.Import;

public sealed record PreferencesImportPlan(TerminalRecordingSettings? Recording, IReadOnlyList<string>? ExclusionPatterns, IReadOnlyList<string> Changes);
