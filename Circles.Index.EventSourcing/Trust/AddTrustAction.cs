using Circles.Index.Graphs;
using Nethermind.Int256;

namespace Circles.Index.EventSourcing.Trust;

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