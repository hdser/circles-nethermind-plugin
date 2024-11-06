using Circles.Pathfinder.Edges;
using Circles.Pathfinder.Graphs;
using Nethermind.Int256;

namespace Circles.Pathfinder.EventSourcing;

public class AddTrustAction(string from, string to, UInt256 expiryTime) : IEventAction<TrustGraph>
{
    readonly TrustEdge _edge = new(from, to, expiryTime);

    public TrustGraph Apply(TrustGraph state)
    {
        if (!state.Nodes.TryGetValue(from, out var fromNode))
        {
            fromNode = state.AddAvatar(from);
        }

        if (!fromNode.OutEdges.Add(_edge))
        {
            throw new InvalidOperationException(
                $"AddTrustAction: Edge {from} -> {to} (expiry: ${expiryTime}). Edge is already an out-edge of {from}.");
        }

        if (!state.Nodes.TryGetValue(to, out var toNode))
        {
            toNode = state.AddAvatar(to);
        }

        if (!toNode.InEdges.Add(_edge))
        {
            throw new InvalidOperationException(
                $"AddTrustAction: Edge {from} -> {to} (expiry: ${expiryTime}). Edge is already an in-edge of {to}.");
        }

        if (!state.Edges.Add(_edge))
        {
            throw new InvalidOperationException(
                $"AddTrustAction: Edge {from} -> {to} (expiry: ${expiryTime}) already exists.");
        }

        return state;
    }

    public IEventAction<TrustGraph> GetInverseAction()
    {
        return new RemoveTrustAction(from, to, expiryTime);
    }
}

public class RemoveTrustAction(string from, string to, UInt256 expiryTime) : IEventAction<TrustGraph>
{
    readonly TrustEdge _edge = new(from, to, expiryTime);

    public TrustGraph Apply(TrustGraph state)
    {
        if (!state.Nodes.TryGetValue(from, out var fromNode))
        {
            throw new InvalidOperationException(
                $"RemoveTrustAction: Edge {from} -> {to} (expiry: ${expiryTime}). From node not found in graph.");
        }

        if (!state.Nodes.TryGetValue(to, out var toNode))
        {
            throw new InvalidOperationException(
                $"RemoveTrustAction: Edge {from} -> {to} (expiry: ${expiryTime}). To node not found in graph.");
        }

        if (!fromNode.OutEdges.Remove(_edge))
        {
            throw new InvalidOperationException(
                $"RemoveTrustAction: Edge {from} -> {to} (expiry: ${expiryTime}). From node doesn't have this out-edge.");
        }

        if (!toNode.InEdges.Remove(_edge))
        {
            throw new InvalidOperationException(
                $"RemoveTrustAction: Edge {from} -> {to} (expiry: ${expiryTime}). To node doesn't have this in-edge.");
        }

        if (!state.Edges.Remove(_edge))
        {
            throw new InvalidOperationException(
                $"RemoveTrustAction: Edge {from} -> {to} (expiry: ${expiryTime}) not found in graph.");
        }

        // If a node is not connected anymore, remove it from the graph.
        if (!fromNode.InEdges.Any() && !fromNode.OutEdges.Any())
        {
            state.RemoveAvatar(from);
        }

        if (!toNode.InEdges.Any() && !toNode.OutEdges.Any())
        {
            state.RemoveAvatar(to);
        }

        return state;
    }

    public IEventAction<TrustGraph> GetInverseAction()
    {
        return new AddTrustAction(from, to, expiryTime);
    }
}