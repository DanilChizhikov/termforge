using DTech.Core.Connections;
using DTech.Core.Terminal;
using DTech.Core.Terminal.Recording;
using DTech.Core.Terminal.Sessions;
using DTech.Core.Tests.SavedConnections.Support;
using NSubstitute;

namespace DTech.Core.Tests.Terminal.Recording.Support;

internal sealed class RecordingTestGraph : IAsyncDisposable
{
    public const string Owner = "owner-session-token";

    public static readonly DateTimeOffset StartTime = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    public static readonly TerminalSize InitialSize = new(100, 30);

    private static readonly SshConnectionTarget Target = new(new SshEndpoint("example.test", 22), "deploy", new AgentAuthentication(null));

    private readonly List<Task> _readers = [];

    public RecordingTestGraph()
    {
        Directory = new TemporaryDirectory();
        TimeProvider = new SettableTimeProvider(StartTime);
        var storageOptions = new TerminalRecordingStorageOptions(Directory.Combine("recordings"));
        Storage = new TerminalRecordingStorage(storageOptions);
        Registry = new TerminalRecordingRegistry();
        RecorderFactory = new TerminalRecorderFactory(Storage, TimeProvider);
        Retention = new TerminalRecordingRetention(Storage, Registry, TimeProvider);
        SettingsStore = new InMemoryRecordingSettingsStore();
        SettingsService = new TerminalRecordingSettingsService(SettingsStore, Registry, Retention);
        RecordingService = new TerminalRecordingService(SettingsService, RecorderFactory, Registry, Retention, TimeProvider);
        Library = new TerminalRecordingLibrary(Storage, Registry);
        var sessionFactory = Substitute.For<ITerminalSessionFactory>();
        sessionFactory.OpenAsync(default!, default).ReturnsForAnyArgs(_ => OpenScriptedSession());
        SessionManager = new TerminalSessionManager(sessionFactory, System.TimeProvider.System, TerminalSessionOptions.Default);
    }

    public TemporaryDirectory Directory { get; }

    public SettableTimeProvider TimeProvider { get; }

    public TerminalRecordingStorage Storage { get; }

    public TerminalRecordingRegistry Registry { get; }

    public TerminalRecorderFactory RecorderFactory { get; }

    public TerminalRecordingRetention Retention { get; }

    public InMemoryRecordingSettingsStore SettingsStore { get; }

    public TerminalRecordingSettingsService SettingsService { get; }

    public TerminalRecordingService RecordingService { get; }

    public TerminalRecordingLibrary Library { get; }

    public TerminalSessionManager SessionManager { get; }

    public List<ScriptedOutputSession> RemoteSessions { get; } = [];

    public void EnableRecording(int maxSizeMegabytes = 10)
    {
        SettingsStore.Stored = TerminalRecordingSettings.Default with { Enabled = true, MaxRecordingSizeMegabytes = maxSizeMegabytes };
    }

    public async Task<ManagedTerminalSession> OpenSessionAsync(string serverName)
    {
        var server = new TerminalServer(Guid.NewGuid(), serverName, Target);
        var request = new TerminalOpenRequest(Owner, server, InitialSize);
        var opened = await SessionManager.OpenAsync(request, CancellationToken.None);
        var session = opened.Value;
        session.TryAttach();
        var reader = ReadUntilClosedAsync(session);
        _readers.Add(reader);
        return session;
    }

    public TerminalRecordingMetadata CreateMetadata(DateTime startedAt)
    {
        var id = TerminalRecordingId.Create();
        return new TerminalRecordingMetadata
        {
            Id = id,
            ConnectionId = Guid.NewGuid(),
            ConnectionName = "web",
            SessionNumber = 1,
            Size = InitialSize,
            StartedAt = startedAt,
            EndedAt = startedAt,
            SizeBytes = 0,
            EndReason = TerminalRecordingEndReason.Stopped,
        };
    }

    public async Task<TerminalRecordingMetadata> StoreFinishedRecordingAsync(DateTime startedAt)
    {
        var metadata = CreateMetadata(startedAt);
        await using (var cast = Storage.CreateCast(metadata.Id))
        {
            var header = AsciicastFormat.Header(metadata);
            await cast.WriteAsync(header);
        }

        await Storage.WriteMetadataAsync(metadata, CancellationToken.None);
        return metadata;
    }

    public static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Registry.StopAllAsync(TerminalRecordingEndReason.SessionClosed);
        await SessionManager.DisposeAsync();
        await Task.WhenAll(_readers);
        Directory.Dispose();
    }

    private SshResult<ITerminalSession> OpenScriptedSession()
    {
        var session = new ScriptedOutputSession();
        RemoteSessions.Add(session);
        return SshResult<ITerminalSession>.Success(session);
    }

    private static async Task ReadUntilClosedAsync(ManagedTerminalSession session)
    {
        var buffer = new byte[16 * 1024];
        try
        {
            var length = await session.ReadAsync(buffer, CancellationToken.None);
            while (length > 0)
            {
                length = await session.ReadAsync(buffer, CancellationToken.None);
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
