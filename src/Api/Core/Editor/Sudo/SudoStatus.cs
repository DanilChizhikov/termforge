namespace DTech.Core.Editor.Sudo;

public sealed record SudoStatus(SudoAvailability Availability, bool HasSavedPassword, string Message);
