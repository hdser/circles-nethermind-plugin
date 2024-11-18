using System.Collections.Immutable;
using Nethermind.Int256;

namespace Circles.Index.Graphs;

public record TrustGraph(
    ImmutableDictionary<string, Node> Nodes,
    ImmutableDictionary<string, AvatarNode> AvatarNodes,
    ImmutableHashSet<TrustEdge> Edges)
    : IGraph<TrustEdge>
{
    public TrustGraph()
        : this(
            ImmutableDictionary<string, Node>.Empty,
            ImmutableDictionary<string, AvatarNode>.Empty,
            ImmutableHashSet<TrustEdge>.Empty)
    {
    }

    public (TrustGraph, AvatarNode) AddAvatar(string avatarAddress)
    {
        var avatar = new AvatarNode(avatarAddress);
        var newAvatarNodes = AvatarNodes.SetItem(avatarAddress, avatar);
        var newNodes = Nodes.SetItem(avatarAddress, avatar);

        var newGraph = this with { AvatarNodes = newAvatarNodes, Nodes = newNodes };

        return (newGraph, avatar);
    }

    public TrustGraph RemoveAvatar(string avatarAddress)
    {
        if (!AvatarNodes.TryGetValue(avatarAddress, out var avatarNode))
        {
            throw new Exception("Avatar not found in graph.");
        }

        // Remove edges
        var edgesToRemove = avatarNode.InEdges.Concat(avatarNode.OutEdges).ToImmutableHashSet();

        var newEdges = Edges.Except(edgesToRemove.OfType<TrustEdge>()).ToImmutableHashSet();

        var newNodes = Nodes.Remove(avatarAddress);
        var newAvatarNodes = AvatarNodes.Remove(avatarAddress);

        return this with { Nodes = newNodes, AvatarNodes = newAvatarNodes, Edges = newEdges };
    }

    public TrustGraph AddTrustEdge(string truster, string trustee, UInt256 expiryTime)
    {
        truster = truster.ToLower();
        trustee = trustee.ToLower();

        var graph = this;

        if (!AvatarNodes.ContainsKey(truster))
        {
            (graph, _) = graph.AddAvatar(truster);
        }

        if (!AvatarNodes.ContainsKey(trustee))
        {
            (graph, _) = graph.AddAvatar(trustee);
        }

        var trustEdge = new TrustEdge(truster, trustee, expiryTime);

        if (graph.Edges.Contains(trustEdge))
        {
            return graph;
        }

        var newEdges = graph.Edges.Add(trustEdge);

        var trusterNode = graph.AvatarNodes[truster];
        var trusteeNode = graph.AvatarNodes[trustee];

        var updatedTrusterNode = trusterNode with { OutEdges = trusterNode.OutEdges.Add(trustEdge) };
        var updatedTrusteeNode = trusteeNode with { InEdges = trusteeNode.InEdges.Add(trustEdge) };

        var newAvatarNodes = graph.AvatarNodes
            .SetItem(truster, updatedTrusterNode)
            .SetItem(trustee, updatedTrusteeNode);

        var newNodes = graph.Nodes
            .SetItem(truster, updatedTrusterNode)
            .SetItem(trustee, updatedTrusteeNode);

        return graph with { Edges = newEdges, AvatarNodes = newAvatarNodes, Nodes = newNodes };
    }
}
