using DTech.Core.Connections;
using DTech.Persistence.Database;
using DTech.Persistence.HostKeys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DTech.HostKeys.Trust;

internal sealed class TrustedHostKeyStore(IServiceScopeFactory scopeFactory, TimeProvider timeProvider) : ITrustedHostKeyStore
{
    public IReadOnlyList<HostKey> GetTrustedHostKeys(SshEndpoint endpoint)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TermForgeDbContext>();
        var host = NormalizeHost(endpoint.Host);
        return context.TrustedHostKeys
            .AsNoTracking()
            .Where(record => record.Host == host && record.Port == endpoint.Port)
            .OrderBy(record => record.Id)
            .Select(record => new HostKey(record.Algorithm, record.Fingerprint))
            .ToList();
    }

    public async Task AddAsync(HostKeyVerificationRequest request, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TermForgeDbContext>();
        var host = NormalizeHost(request.Endpoint.Host);
        var hostKey = request.HostKey;
        var alreadyTrusted = await context.TrustedHostKeys.AnyAsync(
            record => record.Host == host && record.Port == request.Endpoint.Port && record.Algorithm == hostKey.Algorithm && record.Fingerprint == hostKey.Sha256Fingerprint,
            cancellationToken);
        if (alreadyTrusted)
        {
            return;
        }

        var record = CreateRecord(request);
        context.TrustedHostKeys.Add(record);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceAsync(HostKeyVerificationRequest request, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TermForgeDbContext>();
        var host = NormalizeHost(request.Endpoint.Host);
        var algorithm = request.HostKey.Algorithm;
        var replacedRecords = await context.TrustedHostKeys
            .Where(record => record.Host == host && record.Port == request.Endpoint.Port && record.Algorithm == algorithm)
            .ToListAsync(cancellationToken);
        context.TrustedHostKeys.RemoveRange(replacedRecords);
        var record = CreateRecord(request);
        context.TrustedHostKeys.Add(record);
        await context.SaveChangesAsync(cancellationToken);
    }

    private TrustedHostKeyRecord CreateRecord(HostKeyVerificationRequest request)
    {
        var trustedAt = timeProvider.GetUtcNow().UtcDateTime;
        return new TrustedHostKeyRecord
        {
            Host = NormalizeHost(request.Endpoint.Host),
            Port = request.Endpoint.Port,
            Algorithm = request.HostKey.Algorithm,
            Fingerprint = request.HostKey.Sha256Fingerprint,
            TrustedAt = trustedAt,
        };
    }

    private static string NormalizeHost(string host)
    {
        return host.ToLowerInvariant();
    }
}
