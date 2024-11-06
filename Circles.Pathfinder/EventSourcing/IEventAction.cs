namespace Circles.Pathfinder.EventSourcing;

public interface IEventAction<TState>
{
    TState Apply(TState state);
    IEventAction<TState> GetInverseAction();
}