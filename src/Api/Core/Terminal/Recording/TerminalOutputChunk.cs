namespace DTech.Core.Terminal.Recording;

internal readonly record struct TerminalOutputChunk(long Timestamp, byte[] Data);
