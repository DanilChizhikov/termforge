using System.Security.Cryptography;
using System.Text.Json;
using DTech.Core.NetworkDiagnostics;
using DTech.Core.Safety;
using DTech.Core.SavedConnections;

namespace DTech.Entrypoint.NetworkDiagnostics;

public sealed record NetworkDiagnosticPreparation(NetworkDiagnosticPlan Plan, ConfirmationGrant? Confirmation);

public sealed class NetworkDiagnosticService(ISavedConnectionRepository connections, IConfirmationTokenStore confirmations, NetworkDiagnosticStore store)
{
    public async Task<NetworkDiagnosticPreparation> PrepareAsync(NetworkDiagnosticRequest request, CancellationToken cancellationToken)
    {
        var plan = await PlanAsync(request, cancellationToken);
        ConfirmationGrant? grant = null;
        if (plan.FullScan)
        {
            var scope = Scope(plan);
            grant = confirmations.Issue(scope);
        }
        return new NetworkDiagnosticPreparation(plan, grant);
    }

    public async Task<NetworkDiagnosticSnapshot> StartAsync(NetworkDiagnosticRequest request, CancellationToken cancellationToken)
    {
        var plan = await PlanAsync(request, cancellationToken);
        if (plan.FullScan)
        {
            var scope = Scope(plan);
            if (!confirmations.TryConsume(scope, request.ConfirmationToken)) throw new UnauthorizedAccessException("A full port scan requires a fresh confirmation for this exact plan.");
        }
        var run = store.Create(plan);
        return run.Snapshot();
    }

    private async Task<NetworkDiagnosticPlan> PlanAsync(NetworkDiagnosticRequest request, CancellationToken cancellationToken)
    {
        if (request.TargetConnectionId is { } targetId)
        {
            if (!string.IsNullOrWhiteSpace(request.Host)) throw new ArgumentException("Specify either targetConnectionId or host.");
            var target = await connections.FindAsync(targetId, cancellationToken) ?? throw new ArgumentException("The saved target was not found.");
            request = request with { Host = target.Details.Host, Ports = request.Ports ?? $"{target.Details.Port},80,443" };
        }
        var plan = NetworkDiagnosticPlanner.Create(request);
        for (var i = 0; i < plan.Points.Count; i++)
        {
            if (plan.Points[i].ConnectionId is { } pointId && await connections.FindAsync(pointId, cancellationToken) is null)
                throw new ArgumentException("A selected SSH point was not found.");
        }
        return plan;
    }

    private static ConfirmationScope Scope(NetworkDiagnosticPlan plan)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(plan);
        var hash = SHA256.HashData(bytes);
        var key = Convert.ToHexStringLower(hash);
        return new ConfirmationScope(DangerousOperation.NetworkFullScan, Guid.Empty, key);
    }
}
