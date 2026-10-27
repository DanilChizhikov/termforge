using DTech.Core.SshConfig;

namespace DTech.Core.SavedConnections;

public sealed class SshConfigImporter
{
    private readonly SshConfigParser _parser;
    private readonly ISavedConnectionRepository _repository;
    private readonly SshConfigImportOptions _options;
    private readonly TimeProvider _timeProvider;

    public SshConfigImporter(SshConfigParser parser, ISavedConnectionRepository repository, SshConfigImportOptions options, TimeProvider timeProvider)
    {
        _parser = parser;
        _repository = repository;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<SavedConnectionResult<SshConfigImportResult>> ImportAsync(CancellationToken cancellationToken)
    {
        var parsed = _parser.Parse(_options.ConfigPath);
        if (!parsed.FileFound)
        {
            var message = $"No SSH config file was found at '{_options.ConfigPath}'.";
            var missing = new SavedConnectionError(SavedConnectionErrorCode.SshConfigNotFound, message);
            return SavedConnectionResult<SshConfigImportResult>.Failure(missing);
        }

        var issues = new List<SshConfigIssue>(parsed.Issues);
        var existing = await _repository.ListAsync(cancellationToken);
        var plan = BuildPlan(parsed.Hosts, existing);
        var counts = await ApplyPlanAsync(plan, existing, cancellationToken);
        await ApplyJumpHostsAsync(plan, issues, cancellationToken);
        var result = new SshConfigImportResult(_options.ConfigPath, counts.Created, counts.Updated, issues);
        return SavedConnectionResult<SshConfigImportResult>.Success(result);
    }

    private static List<PlannedHost> BuildPlan(IReadOnlyList<SshConfigHostEntry> hosts, IReadOnlyList<SavedConnection> existing)
    {
        var plan = new List<PlannedHost>();
        for (var i = 0; i < hosts.Count; i++)
        {
            var match = FindByAlias(existing, hosts[i].Alias);
            plan.Add(new PlannedHost(hosts[i], match, match?.Id ?? Guid.NewGuid()));
        }

        return plan;
    }

    private static SavedConnection? FindByAlias(IReadOnlyList<SavedConnection> connections, string alias)
    {
        for (var i = 0; i < connections.Count; i++)
        {
            if (string.Equals(connections[i].Details.SshAlias, alias, StringComparison.OrdinalIgnoreCase))
            {
                return connections[i];
            }
        }

        return null;
    }

    private async Task<ImportCounts> ApplyPlanAsync(IReadOnlyList<PlannedHost> plan, IReadOnlyList<SavedConnection> existing, CancellationToken cancellationToken)
    {
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < existing.Count; i++)
        {
            usedNames.Add(existing[i].Details.Name);
        }

        var created = 0;
        var updated = 0;
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        for (var i = 0; i < plan.Count; i++)
        {
            var planned = plan[i];
            if (planned.Existing is null)
            {
                var name = PickUniqueName(planned.Entry.Alias, usedNames);
                var connection = CreateConnection(planned, name, now);
                await _repository.AddAsync(connection, cancellationToken);
                created++;
                continue;
            }

            var refreshed = Refresh(planned.Existing, planned.Entry, now);
            if (refreshed != planned.Existing)
            {
                await _repository.UpdateAsync(refreshed, cancellationToken);
                updated++;
            }
        }

        return new ImportCounts(created, updated);
    }

    private static string PickUniqueName(string alias, HashSet<string> usedNames)
    {
        var candidate = alias;
        var attempt = 1;
        while (!usedNames.Add(candidate))
        {
            candidate = attempt == 1 ? $"{alias} (ssh config)" : $"{alias} (ssh config {attempt})";
            attempt++;
        }

        return candidate;
    }

    private static SavedConnection CreateConnection(PlannedHost planned, string name, DateTime now)
    {
        var draft = ToDraft(planned.Entry, name);
        return new SavedConnection
        {
            Id = planned.Id,
            Details = draft,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static SavedConnection Refresh(SavedConnection existing, SshConfigHostEntry entry, DateTime now)
    {
        var imported = ToDraft(entry, existing.Details.Name);
        var keepsPassword = existing.Details.AuthMethod == ConnectionAuthMethod.Password;
        var details = existing.Details with
        {
            Host = imported.Host,
            Port = imported.Port,
            UserName = imported.UserName,
            AuthMethod = keepsPassword ? existing.Details.AuthMethod : imported.AuthMethod,
            PrivateKeyPath = keepsPassword ? null : imported.PrivateKeyPath,
        };
        return details == existing.Details ? existing : existing with { Details = details, UpdatedAt = now };
    }

    private static SavedConnectionDraft ToDraft(SshConfigHostEntry entry, string name)
    {
        var hasKey = entry.IdentityFile is not null;
        return new SavedConnectionDraft
        {
            Name = name,
            Host = entry.HostName,
            Port = entry.Port,
            UserName = entry.UserName,
            AuthMethod = hasKey ? ConnectionAuthMethod.KeyFile : ConnectionAuthMethod.Agent,
            PrivateKeyPath = entry.IdentityFile,
            SshAlias = entry.Alias,
        };
    }

    private async Task ApplyJumpHostsAsync(IReadOnlyList<PlannedHost> plan, List<SshConfigIssue> issues, CancellationToken cancellationToken)
    {
        var current = (await _repository.ListAsync(cancellationToken)).ToList();
        for (var i = 0; i < plan.Count; i++)
        {
            var planned = plan[i];
            var connectionIndex = current.FindIndex(connection => connection.Id == planned.Id);
            var connection = current[connectionIndex];
            var jumpHostId = ResolveJumpHost(planned, plan, current, issues);
            if (connection.Details.JumpHostId == jumpHostId)
            {
                continue;
            }

            var details = connection.Details with { JumpHostId = jumpHostId };
            var updated = connection with { Details = details, UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime };
            await _repository.UpdateAsync(updated, cancellationToken);
            current[connectionIndex] = updated;
        }
    }

    private Guid? ResolveJumpHost(PlannedHost planned, IReadOnlyList<PlannedHost> plan, IReadOnlyList<SavedConnection> current, List<SshConfigIssue> issues)
    {
        var proxyJump = planned.Entry.ProxyJump;
        if (proxyJump is null)
        {
            return null;
        }

        var reason = FindJumpProblem(proxyJump, plan, out var jumpId);
        if (reason is null && jumpId is not null)
        {
            var guardError = JumpHostGuard.Check(planned.Id, jumpId, current);
            reason = guardError?.Message;
        }

        if (reason is null)
        {
            return jumpId;
        }

        var issue = new SshConfigIssue(_options.ConfigPath, 0, "ProxyJump", reason)
        {
            AffectedAliases = [planned.Entry.Alias],
        };
        issues.Add(issue);
        return null;
    }

    private static string? FindJumpProblem(string proxyJump, IReadOnlyList<PlannedHost> plan, out Guid? jumpId)
    {
        jumpId = null;
        if (proxyJump.Contains(','))
        {
            return $"ProxyJump '{proxyJump}' lists several hops; only a single hop that is a Host alias of the config is supported, so no jump host was set.";
        }

        for (var i = 0; i < plan.Count; i++)
        {
            if (string.Equals(plan[i].Entry.Alias, proxyJump, StringComparison.OrdinalIgnoreCase))
            {
                jumpId = plan[i].Id;
                return null;
            }
        }

        return $"ProxyJump '{proxyJump}' is not a Host alias of the config; only aliases are supported, so no jump host was set.";
    }

    private sealed record PlannedHost(SshConfigHostEntry Entry, SavedConnection? Existing, Guid Id);

    private readonly record struct ImportCounts(int Created, int Updated);
}
