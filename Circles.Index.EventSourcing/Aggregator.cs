using Circles.Index.Common;

namespace Circles.Index.EventSourcing;

/// <summary>
/// Acts like 'reduce()' but for a stream of events instead of a collection.
/// It maps each event to an action and applies that to the current state.
/// Actions can be reversed and the Aggregator can store a limited history of events.
/// </summary>
/// <typeparam name="TState">The type of the aggregate state.</typeparam>
/// <typeparam name="TEvent">The type of the events that are processed.</typeparam>
public class Aggregator<TEvent, TState> : IAggregator<TEvent, TState>
    where TEvent : IIndexEvent
    where TState : class
{
    /// <summary>
    /// The aggregate state.
    /// </summary>
    private TState _state;

    /// <summary>
    /// A limited size log of events that have been applied to the aggregate.
    /// </summary>
    private readonly BlockEventLog<TEvent, TState> _blockEventLog;

    /// <summary>
    /// Function that maps an event to an action that can be applied to the state.
    /// </summary>
    private readonly Func<TEvent, TState, IEnumerable<IEventAction<TState>>> _mapEventToActions;

    /// <summary>
    /// Creates a new Aggregator.
    /// </summary>
    /// <param name="mapEventToActions">A function that maps an event to an action that can be applied to the state.</param>
    /// <param name="initialState">The initial state of the aggregate.</param>
    /// <param name="maxLogSize">The maximum number of blocks to store in the event log (revertible back to N blocks).</param>
    public Aggregator(Func<TEvent, TState, IEnumerable<IEventAction<TState>>> mapEventToActions, TState initialState,
        int maxLogSize)
    {
        _mapEventToActions = mapEventToActions;
        _state = initialState;
        _blockEventLog = new BlockEventLog<TEvent, TState>(maxLogSize);
    }

    /// <summary>
    /// Gets the current aggregate state.
    /// </summary>
    /// <returns>The current aggregate state.</returns>
    public TState GetState()
    {
        return _state;
    }

    /// <summary>
    /// Maps an event to an action and applies it to the current state.
    /// </summary>
    /// <param name="indexEvent">The event to process.</param>
    /// <returns>The new state of the aggregate after applying the event.</returns>
    public TState ProcessEvent(TEvent indexEvent)
    {
        var actions = _mapEventToActions(indexEvent, _state).ToArray();

        TState newState = _state;

        foreach (var action in actions)
        {
            newState = action.Apply(newState);
        }

        _blockEventLog.AddEvent(indexEvent, actions);

        Interlocked.Exchange(ref _state, newState);

        return newState;
    }

    /// <summary>
    /// Applies the inverse of the logged events in reverse order to the state until the specified block number.
    /// Throws an exception if the block number is outside the range of the event log.
    /// </summary>
    /// <param name="blockNumber">The block number to revert to.</param>
    /// <param name="_">Timestamp not used</param>
    public TState RevertToBlock(long blockNumber, long _)
    {
        var eventsToRevert =
            _blockEventLog
                .GetEventsSince(blockNumber)
                .Reverse();

        TState newState = _state;

        foreach (var eventToRevert in eventsToRevert)
        {
            foreach (var actionToRevert in eventToRevert.Actions)
            {
                var inverseAction = actionToRevert.GetInverseAction();
                newState = inverseAction.Apply(newState);
            }
        }

        Interlocked.Exchange(ref _state, newState);

        return newState;
    }
}