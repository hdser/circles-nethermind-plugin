using Circles.Index.Graphs;
using Nethermind.Int256;

namespace Circles.Index.EventSourcing.Trust;

public class RemoveTrustAction(string from, string to, UInt256 expiryTime) : IEventAction<TrustGraph>
{
    readonly TrustEdge _edge = new(from, to, expiryTime);

    public TrustGraph Apply(TrustGraph state)
    {
        var graph = state;

        if (!graph.Nodes.TryGetValue(from, out var fromNode))
        {
            throw new InvalidOperationException(
                $"RemoveTrustAction: Edge {from} -> {to} (expiry: {expiryTime}). From node not found in graph.");
        }

        if (!graph.Nodes.TryGetValue(to, out var toNode))
        {
            throw new InvalidOperationException(
                $"RemoveTrustAction: Edge {from} -> {to} (expiry: {expiryTime}). To node not found in graph.");
        }

        if (!fromNode.OutEdges.Contains(_edge))
        {
            throw new InvalidOperationException(
                $"RemoveTrustAction: Edge {from} -> {to} (expiry: {expiryTime}). From node doesn't have this out-edge.");
        }

        if (!toNode.InEdges.Contains(_edge))
        {
            throw new InvalidOperationException(
                $"RemoveTrustAction: Edge {from} -> {to} (expiry: {expiryTime}). To node doesn't have this in-edge.");
        }

        var newEdges = graph.Edges.Remove(_edge);

        var updatedFromNode = fromNode with { OutEdges = fromNode.OutEdges.Remove(_edge) };
        var updatedToNode = toNode with { InEdges = toNode.InEdges.Remove(_edge) };

        var newNodes = graph.Nodes
            .SetItem(from, updatedFromNode)
            .SetItem(to, updatedToNode);

        var newAvatarNodes = graph.AvatarNodes
            .SetItem(from, (AvatarNode)updatedFromNode)
            .SetItem(to, (AvatarNode)updatedToNode);

        graph = new TrustGraph(newNodes, newAvatarNodes, newEdges);

        // If a node is not connected anymore, remove it from the graph.
        if (!updatedFromNode.InEdges.Any() && !updatedFromNode.OutEdges.Any())
        {
            graph = graph.RemoveAvatar(from);
        }

        if (!updatedToNode.InEdges.Any() && !updatedToNode.OutEdges.Any())
        {
            graph = graph.RemoveAvatar(to);
        }

        return graph;
    }

    public IEventAction<TrustGraph> GetInverseAction()
    {
        return new AddTrustAction(from, to, expiryTime);
    }
}