using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DTech.Credentials.Tests.Support;

public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<RecordedLogEntry> _entries = new();

    public IReadOnlyList<RecordedLogEntry> Entries => _entries.ToArray();

    public string AllText => string.Join(Environment.NewLine, Entries);

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        var exceptionText = exception?.ToString() ?? string.Empty;
        var entry = new RecordedLogEntry(logLevel, message, exceptionText);
        _entries.Enqueue(entry);
    }
}

public sealed record RecordedLogEntry(LogLevel Level, string Message, string Exception);
