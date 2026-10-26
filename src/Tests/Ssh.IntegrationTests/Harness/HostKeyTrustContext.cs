using System.Security.Cryptography;
using System.Text;
using DTech.Core.Connections;
using DTech.HostKeys;
using DTech.HostKeys.Decisions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DTech.Ssh.IntegrationTests.Harness;

internal sealed class HostKeyTrustContext : IAsyncDisposable
{
    private static readonly TimeSpan PendingWaitLimit = TimeSpan.FromSeconds(30);

    private readonly string _directory;
    private readonly ServiceProvider _services;

    private HostKeyTrustContext(string directory, ServiceProvider services, InMemoryTrustedHostKeyStore trustedStore)
    {
        _directory = directory;
        _services = services;
        TrustedStore = trustedStore;
    }

    public string KnownHostsPath => Path.Combine(_directory, "known_hosts");

    public InMemoryTrustedHostKeyStore TrustedStore { get; }

    public HostKeyDecisionBroker Broker => _services.GetRequiredService<HostKeyDecisionBroker>();

    public HostKeyDecisionHandler Handler => _services.GetRequiredService<HostKeyDecisionHandler>();

    public static HostKeyTrustContext Create(TimeSpan decisionTimeout)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"termforge-host-key-flow-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var knownHostsPath = Path.Combine(directory, "known_hosts");
        var settings = new Dictionary<string, string?>
        {
            [$"{HostKeyVerificationOptions.SectionName}:{nameof(HostKeyVerificationOptions.KnownHostsFiles)}:0"] = knownHostsPath,
            [$"{HostKeyVerificationOptions.SectionName}:{nameof(HostKeyVerificationOptions.DecisionTimeout)}"] = decisionTimeout.ToString(),
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var trustedStore = new InMemoryTrustedHostKeyStore();
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddHostKeyVerification(configuration);
        collection.AddSingleton<ITrustedHostKeyStore>(trustedStore);
        collection.AddSshTransport();
        var services = collection.BuildServiceProvider();
        return new HostKeyTrustContext(directory, services, trustedStore);
    }

    public TService Get<TService>()
        where TService : notnull
    {
        return _services.GetRequiredService<TService>();
    }

    public void WriteKnownHosts(params string[] lines)
    {
        File.WriteAllLines(KnownHostsPath, lines);
    }

    public static string CreateHashedHost(SshEndpoint endpoint)
    {
        var lookupName = endpoint.Port == SshEndpoint.DefaultPort ? endpoint.Host : $"[{endpoint.Host}]:{endpoint.Port}";
        var salt = RandomNumberGenerator.GetBytes(20);
        var lookupBytes = Encoding.ASCII.GetBytes(lookupName);
        var hash = HMACSHA1.HashData(salt, lookupBytes);
        var encodedSalt = Convert.ToBase64String(salt);
        var encodedHash = Convert.ToBase64String(hash);
        return $"|1|{encodedSalt}|{encodedHash}";
    }

    public async Task<PendingHostKeyDecision> WaitForPendingAsync(SshEndpoint endpoint)
    {
        using var timeout = new CancellationTokenSource(PendingWaitLimit);
        while (true)
        {
            var pending = Broker.GetPending();
            var match = pending.FirstOrDefault(decision => decision.Challenge.Request.Endpoint == endpoint);
            if (match is not null)
            {
                return match;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
        }
    }

    public Task<HostKeyDecisionResult> DecideAsync(PendingHostKeyDecision pending, HostKeyUserDecision decision, bool confirmReplace = false)
    {
        var command = new HostKeyDecisionCommand(pending.Id, decision, confirmReplace);
        return Handler.DecideAsync(command, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
