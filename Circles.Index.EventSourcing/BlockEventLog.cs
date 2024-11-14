using System.Collections.Concurrent;
using Circles.Index.Common;

namespace Circles.Index.EventSourcing;

/// <summary>
/// A capped-size log of events that have been applied to an aggregate.
/// Stores the events and corresponding actions to enable reverting to a previous state.
/// </summary>
/// <typeparam name="TState">The type of the state that the events are applied to.</typeparam>
/// <typeparam name="TEvent">The type of the events that are stored.</typeparam>
public class BlockEventLog<TEvent, TState>(long maxBlocks)
    where TEvent : IIndexEvent
{
    private readonly ConcurrentDictionary<long, ConcurrentQueue<(TEvent, IEventAction<TState>[])>>
        _eventsPerBlock = new();

    private readonly ConcurrentQueue<long> _blockNumbers = new();

    /// <summary>
    /// Adds a new event and the corresponding actions to the log.
    /// </summary>
    /// <param name="indexEvent">The event.</param>
    /// <param name="actions">The actions that were applied to the state.</param>
    public void AddEvent(TEvent indexEvent, IEventAction<TState>[] actions)
    {
        _eventsPerBlock.AddOrUpdate(
            indexEvent.BlockNumber,
            bn =>
            {
                _blockNumbers.Enqueue(bn);
                return new ConcurrentQueue<(TEvent, IEventAction<TState>[])>([(indexEvent, actions)]);
            },
            (_, queue) =>
            {
                queue.Enqueue((indexEvent, actions));
                return queue;
            });

        while (_blockNumbers.Count > maxBlocks)
        {
            if (_blockNumbers.TryDequeue(out var oldestBlock))
            {
                _eventsPerBlock.TryRemove(oldestBlock, out _);
            }
        }
    }

    /// <summary>
    /// Returns all events and actions that have been applied since the given block number.
    /// </summary>
    /// <param name="blockNumber"></param>
    /// <returns></returns>
    public IEnumerable<(TEvent Event, IEventAction<TState>[] Actions)> GetEventsSince(long blockNumber)
    {
        if (blockNumber < MinBlockNumber)
        {
            throw new ArgumentOutOfRangeException(nameof(blockNumber),
                $"Block number is too low. The event log starts at block {MinBlockNumber}.");
        }

        if (blockNumber > MaxBlockNumber)
        {
            throw new ArgumentOutOfRangeException(nameof(blockNumber),
                $"Block number is too high. The event log ends at block {MaxBlockNumber}.");
        }

        return _eventsPerBlock
            .Where(kvp => kvp.Key > blockNumber)
            .OrderBy(kvp => kvp.Key)
            .SelectMany(kvp => kvp.Value);
    }

    public long? MinBlockNumber
    {
        get
        {
            var keys = _eventsPerBlock.Keys.ToArray();
            if (keys.Length == 0)
                return null;
            return keys.Min();
        }
    }

    public long? MaxBlockNumber
    {
        get
        {
            var keys = _eventsPerBlock.Keys.ToArray();
            if (keys.Length == 0)
                return null;
            return keys.Max();
        }
    }
}