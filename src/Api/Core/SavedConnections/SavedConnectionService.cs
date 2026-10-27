namespace DTech.Core.SavedConnections;

public sealed class SavedConnectionService
{
    private readonly ISavedConnectionRepository _repository;
    private readonly ConnectionPasswordStore _passwordStore;
    private readonly TimeProvider _timeProvider;

    public SavedConnectionService(ISavedConnectionRepository repository, ConnectionPasswordStore passwordStore, TimeProvider timeProvider)
    {
        _repository = repository;
        _passwordStore = passwordStore;
        _timeProvider = timeProvider;
    }

    public Task<IReadOnlyList<SavedConnection>> ListAsync(CancellationToken cancellationToken)
    {
        return _repository.ListAsync(cancellationToken);
    }

    public async Task<SavedConnectionResult<SavedConnection>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var connection = await _repository.FindAsync(id, cancellationToken);
        if (connection is null)
        {
            return SavedConnectionResult<SavedConnection>.Failure(SavedConnectionError.NotFound(id));
        }

        return SavedConnectionResult<SavedConnection>.Success(connection);
    }

    public async Task<SavedConnectionResult<SavedConnection>> CreateAsync(SavedConnectionSubmission submission, CancellationToken cancellationToken)
    {
        var draft = SavedConnectionValidator.Normalize(submission.Draft);
        var id = Guid.NewGuid();
        var all = await _repository.ListAsync(cancellationToken);
        var error = SavedConnectionValidator.Validate(draft) ?? CheckAgainstExisting(id, draft, all);
        if (error is not null)
        {
            return SavedConnectionResult<SavedConnection>.Failure(error);
        }

        var password = await ApplyPasswordAsync(id, draft, submission.Password, false, cancellationToken);
        if (!password.IsSuccess)
        {
            return SavedConnectionResult<SavedConnection>.Failure(password.Error);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var connection = new SavedConnection
        {
            Id = id,
            Details = draft,
            HasPassword = password.Value,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await _repository.AddAsync(connection, cancellationToken);
        return SavedConnectionResult<SavedConnection>.Success(connection);
    }

    public async Task<SavedConnectionResult<SavedConnection>> UpdateAsync(Guid id, SavedConnectionSubmission submission, CancellationToken cancellationToken)
    {
        var existing = await _repository.FindAsync(id, cancellationToken);
        if (existing is null)
        {
            return SavedConnectionResult<SavedConnection>.Failure(SavedConnectionError.NotFound(id));
        }

        var draft = SavedConnectionValidator.Normalize(submission.Draft);
        var all = await _repository.ListAsync(cancellationToken);
        var error = SavedConnectionValidator.Validate(draft) ?? CheckAgainstExisting(id, draft, all);
        if (error is not null)
        {
            return SavedConnectionResult<SavedConnection>.Failure(error);
        }

        var password = await ApplyPasswordAsync(id, draft, submission.Password, existing.HasPassword, cancellationToken);
        if (!password.IsSuccess)
        {
            return SavedConnectionResult<SavedConnection>.Failure(password.Error);
        }

        var updated = existing with
        {
            Details = draft,
            HasPassword = password.Value,
            UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime,
        };
        await _repository.UpdateAsync(updated, cancellationToken);
        return SavedConnectionResult<SavedConnection>.Success(updated);
    }

    public async Task<SavedConnectionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var all = await _repository.ListAsync(cancellationToken);
        var existing = JumpHostGuard.Find(all, id);
        if (existing is null)
        {
            return SavedConnectionResult.Failure(SavedConnectionError.NotFound(id));
        }

        var dependent = FindDependent(all, id);
        if (dependent is not null)
        {
            var message = $"'{existing.Details.Name}' is the jump host of '{dependent.Details.Name}'. Change that connection first.";
            var inUse = new SavedConnectionError(SavedConnectionErrorCode.InUse, message);
            return SavedConnectionResult.Failure(inUse);
        }

        if (existing.HasPassword)
        {
            var removed = await _passwordStore.RemoveAsync(id, cancellationToken);
            if (!removed.IsSuccess)
            {
                return removed;
            }
        }

        await _repository.DeleteAsync(id, cancellationToken);
        return SavedConnectionResult.Success();
    }

    public async Task<SavedConnectionResult> RenameGroupAsync(string currentName, string newName, CancellationToken cancellationToken)
    {
        var trimmedName = newName.Trim();
        if (trimmedName.Length == 0 || trimmedName.Length > SavedConnectionValidator.MaxNameLength)
        {
            var invalid = SavedConnectionError.Invalid($"The group name must be 1 to {SavedConnectionValidator.MaxNameLength} characters long.");
            return SavedConnectionResult.Failure(invalid);
        }

        await _repository.RenameGroupAsync(currentName, trimmedName, cancellationToken);
        return SavedConnectionResult.Success();
    }

    public async Task<SavedConnectionResult> RemoveGroupAsync(string name, CancellationToken cancellationToken)
    {
        await _repository.RemoveGroupAsync(name, cancellationToken);
        return SavedConnectionResult.Success();
    }

    private static SavedConnectionError? CheckAgainstExisting(Guid id, SavedConnectionDraft draft, IReadOnlyList<SavedConnection> all)
    {
        for (var i = 0; i < all.Count; i++)
        {
            var other = all[i];
            var sameName = string.Equals(other.Details.Name, draft.Name, StringComparison.OrdinalIgnoreCase);
            if (other.Id != id && sameName)
            {
                return SavedConnectionError.NameTaken(draft.Name);
            }
        }

        return JumpHostGuard.Check(id, draft.JumpHostId, all);
    }

    private static SavedConnection? FindDependent(IReadOnlyList<SavedConnection> all, Guid jumpHostId)
    {
        for (var i = 0; i < all.Count; i++)
        {
            if (all[i].Details.JumpHostId == jumpHostId)
            {
                return all[i];
            }
        }

        return null;
    }

    private async Task<SavedConnectionResult<bool>> ApplyPasswordAsync(Guid id, SavedConnectionDraft draft, string? password, bool hadPassword, CancellationToken cancellationToken)
    {
        var hasNewPassword = !string.IsNullOrEmpty(password);
        if (draft.AuthMethod != ConnectionAuthMethod.Password)
        {
            return await DropPasswordAsync(id, hasNewPassword, hadPassword, cancellationToken);
        }

        if (!hasNewPassword)
        {
            return SavedConnectionResult<bool>.Success(hadPassword);
        }

        var saved = await _passwordStore.SaveAsync(id, password!, cancellationToken);
        return saved.IsSuccess ? SavedConnectionResult<bool>.Success(true) : SavedConnectionResult<bool>.Failure(saved.Error);
    }

    private async Task<SavedConnectionResult<bool>> DropPasswordAsync(Guid id, bool hasNewPassword, bool hadPassword, CancellationToken cancellationToken)
    {
        if (hasNewPassword)
        {
            var error = SavedConnectionError.Invalid("A password can be stored only for password authentication.");
            return SavedConnectionResult<bool>.Failure(error);
        }

        if (!hadPassword)
        {
            return SavedConnectionResult<bool>.Success(false);
        }

        var removed = await _passwordStore.RemoveAsync(id, cancellationToken);
        return removed.IsSuccess ? SavedConnectionResult<bool>.Success(false) : SavedConnectionResult<bool>.Failure(removed.Error);
    }
}
