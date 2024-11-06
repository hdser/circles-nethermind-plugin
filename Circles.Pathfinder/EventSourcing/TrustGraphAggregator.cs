using Circles.Index.CirclesV2;
using Circles.Index.Common;
using Circles.Pathfinder.Edges;
using Circles.Pathfinder.Graphs;

namespace Circles.Pathfinder.EventSourcing;

/// <summary>
/// Aggregates Circles trust events into a trust graph.
/// </summary>
public class TrustGraphAggregator : IAggregator<IIndexEvent, TrustGraph>
{
    private readonly Aggregator<IIndexEvent, TrustGraph> _aggregator;

    /// <summary>
    /// The current time, the aggregator experiences.
    /// Is updated whenever a new event is processed and can only move forward.
    /// </summary>
    private long _currentTimestamp = -1;

    public TrustGraphAggregator()
    {
        _aggregator = new Aggregator<IIndexEvent, TrustGraph>(MapEventToActions, new TrustGraph(), 12);
    }

    public TrustGraph GetState() => _aggregator.GetState();

    public TrustGraph ProcessEvent(IIndexEvent indexEvent) => _aggregator.ProcessEvent(indexEvent);

    public TrustGraph RevertToBlock(long blockNumber, long timestamp)
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
    private IEnumerable<IEventAction<TrustGraph>> MapEventToActions(IIndexEvent @event, TrustGraph state)
    {
        if (@event.Timestamp <= _currentTimestamp)
        {
            throw new InvalidOperationException(
                $"Event timestamp is older than the current timestamp of the aggregator. Current timestamp: {_currentTimestamp}, event timestamp: {@event.Timestamp}");
        }

        _currentTimestamp = @event.Timestamp;

        switch (@event)
        {
            case BlockEvent:
            {
                // Check if trust relationships have expired and clean them up if necessary.
                var expiredTrusts =
                    state
                        .Edges
                        .Where(o => o.ExpiryTime <= _currentTimestamp)
                        .ToArray();

                foreach (var expiredTrust in expiredTrusts)
                {
                    yield return new RemoveTrustAction(expiredTrust.From, expiredTrust.To, expiredTrust.ExpiryTime);
                }

                break;
            }
            case Trust trustEvent:
            {
                if (trustEvent.ExpiryTime > _currentTimestamp)
                {
                    yield return new AddTrustAction(trustEvent.Truster, trustEvent.Trustee, trustEvent.ExpiryTime);
                }
                else if (state.Nodes.TryGetValue(trustEvent.Truster, out var truster)
                         && state.Nodes.TryGetValue(trustEvent.Trustee, out var trustee))
                {
                    var edgeToRemove = truster.OutEdges.Cast<TrustEdge>().First(o => o.To == trustee.Address);
                    yield return new RemoveTrustAction(edgeToRemove.From, edgeToRemove.To, edgeToRemove.ExpiryTime);
                }

                break;
            }
        }
    }
}