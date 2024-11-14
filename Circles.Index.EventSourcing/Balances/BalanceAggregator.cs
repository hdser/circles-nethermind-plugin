using System.Numerics;
using Circles.Index.CirclesV2;
using Circles.Index.Common;
using Circles.Index.Graphs;

namespace Circles.Index.EventSourcing.Balances;

/// <summary>
/// Aggregates Circles transfer events into a balance graph.
/// </summary>
public class BalanceGraphAggregator : IAggregator<IIndexEvent, BalanceGraph>
{
    private readonly Aggregator<IIndexEvent, BalanceGraph> _aggregator;

    /// <summary>
    /// The current time, the aggregator experiences.
    /// Is updated whenever a new event is processed and can only move forward.
    /// </summary>
    private long _currentTimestamp = -1;

    public BalanceGraphAggregator()
    {
        _aggregator = new Aggregator<IIndexEvent, BalanceGraph>(MapEventToActions, new BalanceGraph(), 12);
    }

    public BalanceGraph GetState() => _aggregator.GetState();

    public BalanceGraph ProcessEvent(IIndexEvent indexEvent) => _aggregator.ProcessEvent(indexEvent);

    public BalanceGraph RevertToBlock(long blockNumber, long timestamp)
    {
        _currentTimestamp = timestamp;
        return _aggregator.RevertToBlock(blockNumber, timestamp);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="event"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private IEnumerable<IEventAction<BalanceGraph>> MapEventToActions(IIndexEvent @event, BalanceGraph state)
    {
        if (@event.Timestamp < _currentTimestamp)
        {
            throw new InvalidOperationException(
                $"Event timestamp is older than the current timestamp of the aggregator. Current timestamp: {_currentTimestamp}, event timestamp: {@event.Timestamp}");
        }

        _currentTimestamp = @event.Timestamp;

        switch (@event)
        {
            case BlockEvent:
            {
                // Only used to track the time, required for the demurrage calculation.
                break;
            }
            case TransferEvent transfer:
            {
                if (transfer.To != "0x0000000000000000000000000000000000000000")
                {
                    var currentToBalance = state.GetBalance(transfer.To, transfer.TokenAddress);
                    var newToBalance = currentToBalance + (BigInteger)transfer.Value;
                    state.SetBalance(transfer.To, transfer.TokenAddress, newToBalance);
                }

                if (transfer.From != "0x0000000000000000000000000000000000000000")
                {
                    var currentFromBalance = state.GetBalance(transfer.From, transfer.TokenAddress);
                    var newFromBalance = currentFromBalance - (BigInteger)transfer.Value;
                    state.SetBalance(transfer.From, transfer.TokenAddress, newFromBalance);
                }

                break;
            }
        }

        yield break;
    }
}