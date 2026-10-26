namespace DTech.Core.Transfers.Execution;

public sealed class TransferStrategySelector
{
    private readonly Dictionary<TransferDirection, ITransferStrategy> _strategies = new();

    public TransferStrategySelector(IEnumerable<ITransferStrategy> strategies)
    {
        foreach (var strategy in strategies)
        {
            _strategies[strategy.Direction] = strategy;
        }
    }

    public ITransferStrategy For(TransferDirection direction)
    {
        return _strategies.TryGetValue(direction, out var strategy)
            ? strategy
            : throw new InvalidOperationException($"No transfer strategy is registered for {direction}.");
    }
}
