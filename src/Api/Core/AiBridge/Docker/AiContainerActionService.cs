using DTech.Core.AiBridge.Approvals;
using DTech.Core.Docker;
using DTech.Core.Safety;

namespace DTech.Core.AiBridge.Docker;

public sealed class AiContainerActionService
{
    private readonly AiServerAccessPolicy _accessPolicy;
    private readonly AiActionGate _actionGate;
    private readonly ContainerActionService _actionService;
    private readonly IConfirmationTokenStore _confirmations;

    public AiContainerActionService(AiServerAccessPolicy accessPolicy, AiActionGate actionGate, ContainerActionService actionService, IConfirmationTokenStore confirmations)
    {
        _accessPolicy = accessPolicy;
        _actionGate = actionGate;
        _actionService = actionService;
        _confirmations = confirmations;
    }

    public async Task<AiBridgeResult<ContainerActionResult>> RunAsync(AiContainerActionRequest request, CancellationToken cancellationToken)
    {
        if (!ContainerReference.TryParse(request.Container, out var container))
        {
            var invalidContainer = DockerError.InvalidContainer();
            var invalid = AiBridgeErrorMapper.FromDocker(invalidContainer);
            return AiBridgeResult<ContainerActionResult>.Failure(invalid);
        }

        var access = await _accessPolicy.CheckWritableAsync(request.ConnectionId, cancellationToken);
        if (!access.IsSuccess)
        {
            return AiBridgeResult<ContainerActionResult>.Failure(access.Error);
        }

        var actionName = request.Action.ToString().ToLowerInvariant();
        var summary = $"docker {actionName} {container.Value}";
        var serverName = access.Value.Details.Name;
        var action = new AiActionDescription(request.ConnectionId, serverName, AiActionKind.ContainerAction, summary);
        var rejection = await _actionGate.AuthorizeAsync(action, cancellationToken);
        if (rejection is not null)
        {
            return AiBridgeResult<ContainerActionResult>.Failure(rejection);
        }

        var unconfirmedRequest = new ContainerActionRequest(request.ConnectionId, container.Value, request.Action);
        var confirmationToken = IssueConfirmation(unconfirmedRequest);
        var actionRequest = unconfirmedRequest with { ConfirmationToken = confirmationToken };
        var completed = await _actionService.RunAsync(actionRequest, cancellationToken);
        if (!completed.IsSuccess)
        {
            var failure = AiBridgeErrorMapper.FromDocker(completed.Error);
            return AiBridgeResult<ContainerActionResult>.Failure(failure);
        }

        return AiBridgeResult<ContainerActionResult>.Success(completed.Value);
    }

    private string? IssueConfirmation(ContainerActionRequest request)
    {
        if (request.DangerousOperation is not { } operation)
        {
            return null;
        }

        var scope = new ConfirmationScope(operation, request.ConnectionId, request.Container!);
        var grant = _confirmations.Issue(scope);
        return grant.Token;
    }
}
