using System.Numerics;
using System.Collections.Immutable;

namespace Circles.Index.Graphs;

public record CapacityGraph(
    ImmutableDictionary<string, Node> Nodes,
    ImmutableDictionary<string, AvatarNode> AvatarNodes,
    ImmutableDictionary<string, BalanceNode> BalanceNodes,
    ImmutableHashSet<CapacityEdge> Edges)
    : IGraph<CapacityEdge>
{
    public CapacityGraph()
        : this(
            ImmutableDictionary<string, Node>.Empty,
            ImmutableDictionary<string, AvatarNode>.Empty,
            ImmutableDictionary<string, BalanceNode>.Empty,
            ImmutableHashSet<CapacityEdge>.Empty)
    {
    }

    public CapacityGraph AddAvatar(string avatarAddress)
    {
        avatarAddress = avatarAddress.ToLower();
        if (!AvatarNodes.ContainsKey(avatarAddress))
        {
            var avatarNode = new AvatarNode(avatarAddress);
            var newAvatarNodes = AvatarNodes.SetItem(avatarAddress, avatarNode);
            var newNodes = Nodes.SetItem(avatarAddress, avatarNode);

            return this with { AvatarNodes = newAvatarNodes, Nodes = newNodes };
        }

        return this;
    }

    public CapacityGraph AddBalanceNode(string address, string token, BigInteger amount)
    {
        address = address.ToLower();
        token = token.ToLower();

        var balanceNode = new BalanceNode(address, token, amount, 0); // Assuming LastChangeTimestamp is 0
        var newBalanceNodes = BalanceNodes.SetItem(balanceNode.Address, balanceNode);
        var newNodes = Nodes.SetItem(balanceNode.Address, balanceNode);

        return this with { BalanceNodes = newBalanceNodes, Nodes = newNodes };
    }

    public CapacityGraph AddCapacityEdge(string from, string to, string token, BigInteger capacity)
    {
        from = from.ToLower();
        to = to.ToLower();
        token = token.ToLower();

        var edge = new CapacityEdge(from, to, token, capacity);
        var newEdges = Edges.Add(edge);

        var graph = this;

        if (!Nodes.ContainsKey(from))
        {
            graph = graph.AddAvatar(from);
        }

        if (!Nodes.ContainsKey(to))
        {
            graph = graph.AddAvatar(to);
        }

        var fromNode = graph.Nodes[from];
        var toNode = graph.Nodes[to];

        var updatedFromNode = fromNode with { OutEdges = fromNode.OutEdges.Add(edge) };
        var updatedToNode = toNode with { InEdges = toNode.InEdges.Add(edge) };

        var newNodes = graph.Nodes
            .SetItem(from, updatedFromNode)
            .SetItem(to, updatedToNode);

        if (fromNode is AvatarNode)
        {
            var newAvatarNodes = graph.AvatarNodes.SetItem(from, (AvatarNode)updatedFromNode);
            graph = graph with { AvatarNodes = newAvatarNodes };
        }
        else if (fromNode is BalanceNode)
        {
            var newBalanceNodes = graph.BalanceNodes.SetItem(from, (BalanceNode)updatedFromNode);
            graph = graph with { BalanceNodes = newBalanceNodes };
        }

        if (toNode is AvatarNode)
        {
            var newAvatarNodes = graph.AvatarNodes.SetItem(to, (AvatarNode)updatedToNode);
            graph = graph with { AvatarNodes = newAvatarNodes };
        }
        else if (toNode is BalanceNode)
        {
            var newBalanceNodes = graph.BalanceNodes.SetItem(to, (BalanceNode)updatedToNode);
            graph = graph with { BalanceNodes = newBalanceNodes };
        }

        return graph with { Nodes = newNodes, Edges = newEdges };
    }
}
