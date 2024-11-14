using Circles.Index.EventSourcing.Balances;
using Circles.Index.EventSourcing.Trust;

namespace Circles.Index.EventSourcing;

public sealed class Aggregates
{
    public TrustGraphAggregator TrustGraph { get; } = new();
    public BalanceGraphAggregator BalanceGraph { get; } = new();
}