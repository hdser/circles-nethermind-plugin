using System.Numerics;
using System.Collections.Immutable;

namespace Circles.Index.Graphs;

public record BalanceGraph(
    ImmutableDictionary<string, Node> Nodes,
    ImmutableHashSet<CapacityEdge> Edges,
    ImmutableDictionary<string, BalanceNode> BalanceNodes,
    ImmutableDictionary<string, AvatarNode> AvatarNodes)
    : IGraph<CapacityEdge>
{
    public BalanceGraph()
        : this(
            ImmutableDictionary<string, Node>.Empty,
            ImmutableHashSet<CapacityEdge>.Empty,
            ImmutableDictionary<string, BalanceNode>.Empty,
            ImmutableDictionary<string, AvatarNode>.Empty)
    {
    }

    public BalanceGraph AddAvatar(string avatarAddress)
    {
        var avatarNode = new AvatarNode(avatarAddress);
        var newAvatarNodes = AvatarNodes.SetItem(avatarAddress, avatarNode);
        var newNodes = Nodes.SetItem(avatarAddress, avatarNode);

        return this with { AvatarNodes = newAvatarNodes, Nodes = newNodes };
    }

    private BalanceGraph RemoveNode(string key)
    {
        if (!Nodes.TryGetValue(key, out var node))
        {
            return this;
        }

        // Remove all edges connected to the node
        var edgesToRemove = node.OutEdges.Concat(node.InEdges).ToImmutableHashSet();
        var newEdges = Edges.Except(edgesToRemove.OfType<CapacityEdge>()).ToImmutableHashSet();

        var newNodes = Nodes.Remove(key);
        var newBalanceNodes = BalanceNodes.Remove(key);
        var newAvatarNodes = AvatarNodes.Remove(key);

        return new BalanceGraph(newNodes, newEdges, newBalanceNodes, newAvatarNodes);
    }

    public BigInteger GetBalance(string address, string token)
    {
        return BalanceNodes.TryGetValue(address + "-" + token, out var balanceNode)
            ? balanceNode.Amount
            : BigInteger.Zero;
    }

    public BigInteger GetDemurragedBalance(string address, string token)
    {
        return BalanceNodes.TryGetValue(address + "-" + token, out var balanceNode)
            ? balanceNode.DemurragedAmount
            : BigInteger.Zero;
    }

    public BalanceGraph SetBalance(string address, string token, BigInteger balance, long timestamp)
    {
        var graph = this;
        var balanceNodeKey = address + "-" + token;

        if (balance == BigInteger.Zero)
        {
            return SetBalanceToZero(address, token, graph, balanceNodeKey);
        }

        return SetNewBalance(address, token, balance, timestamp, graph, balanceNodeKey);
    }

    private static BalanceGraph SetNewBalance(string address, string token, BigInteger balance, long timestamp,
        BalanceGraph graph, string balanceNodeKey)
    {
        // Ensure the avatar node exists
        if (!graph.AvatarNodes.TryGetValue(address, out var avatarNode))
        {
            graph = graph.AddAvatar(address);
            avatarNode = graph.AvatarNodes[address];
        }

        // Try to get the existing balance node
        BalanceNode balanceNode;
        if (graph.BalanceNodes.TryGetValue(balanceNodeKey, out var existingBalanceNode))
        {
            // Update the amount and timestamp
            balanceNode = existingBalanceNode with { Amount = balance, LastChangeTimestamp = timestamp };
        }
        else
        {
            // Create a new balance node
            balanceNode = new BalanceNode(balanceNodeKey, token, balance, timestamp);
        }

        // Try to find the existing edge from the avatar to the balance node
        var existingEdge = avatarNode.OutEdges.OfType<CapacityEdge>()
            .FirstOrDefault(e => e.To == balanceNodeKey && e.Token == token);

        // Create the capacity edge
        var capacityEdge = new CapacityEdge(address, balanceNode.Address, token, balance);

        // Update the edge collections
        var newEdges = graph.Edges;

        if (existingEdge != null)
        {
            newEdges = newEdges.Remove(existingEdge).Add(capacityEdge);
        }
        else
        {
            newEdges = newEdges.Add(capacityEdge);
        }

        // Update the avatar node's OutEdges
        var updatedAvatarOutEdges = avatarNode.OutEdges;
        if (existingEdge != null)
        {
            updatedAvatarOutEdges = updatedAvatarOutEdges.Remove(existingEdge).Add(capacityEdge);
        }
        else
        {
            updatedAvatarOutEdges = updatedAvatarOutEdges.Add(capacityEdge);
        }

        var updatedAvatarNode = avatarNode with { OutEdges = updatedAvatarOutEdges };

        // Update the balance node's InEdges
        var existingInEdge = balanceNode.InEdges.OfType<CapacityEdge>()
            .FirstOrDefault(e => e.From == address && e.Token == token);

        var updatedBalanceInEdges = balanceNode.InEdges;
        if (existingInEdge != null)
        {
            updatedBalanceInEdges = updatedBalanceInEdges.Remove(existingInEdge).Add(capacityEdge);
        }
        else
        {
            updatedBalanceInEdges = updatedBalanceInEdges.Add(capacityEdge);
        }

        var updatedBalanceNode = balanceNode with { InEdges = updatedBalanceInEdges };

        // Update Nodes
        var newNodes = graph.Nodes
            .SetItem(avatarNode.Address, updatedAvatarNode)
            .SetItem(balanceNode.Address, updatedBalanceNode);

        var newAvatarNodes = graph.AvatarNodes.SetItem(avatarNode.Address, updatedAvatarNode);
        var newBalanceNodes = graph.BalanceNodes.SetItem(balanceNode.Address, updatedBalanceNode);

        // Return the new graph
        return graph with
        {
            Edges = newEdges, Nodes = newNodes, AvatarNodes = newAvatarNodes, BalanceNodes = newBalanceNodes
        };
    }

    private static BalanceGraph SetBalanceToZero(string address, string token, BalanceGraph graph,
        string balanceNodeKey)
    {
        // Remove the balance node and associated edges
        if (!graph.BalanceNodes.TryGetValue(balanceNodeKey, out var existingBalanceNode))
        {
            // Balance node doesn't exist; nothing to remove
            return graph;
        }

        // Remove the edge from the avatar node to the balance node
        if (!graph.AvatarNodes.TryGetValue(address, out var avatarNode))
        {
            // Avatar node doesn't exist; nothing to remove
            return graph;
        }

        var existingEdge = avatarNode.OutEdges.OfType<CapacityEdge>()
            .FirstOrDefault(e => e.To == balanceNodeKey && e.Token == token);

        var newEdges = graph.Edges;
        var updatedAvatarOutEdges = avatarNode.OutEdges;
        var updatedBalanceInEdges = existingBalanceNode.InEdges;

        if (existingEdge != null)
        {
            newEdges = newEdges.Remove(existingEdge);
            updatedAvatarOutEdges = updatedAvatarOutEdges.Remove(existingEdge);
            updatedBalanceInEdges = updatedBalanceInEdges.Remove(existingEdge);
        }

        var updatedAvatarNode = avatarNode with { OutEdges = updatedAvatarOutEdges };
        var updatedBalanceNode = existingBalanceNode with { InEdges = updatedBalanceInEdges };

        // Update Nodes, AvatarNodes, BalanceNodes
        var newNodes = graph.Nodes.SetItem(avatarNode.Address, updatedAvatarNode);
        var newAvatarNodes = graph.AvatarNodes.SetItem(avatarNode.Address, updatedAvatarNode);
        var newBalanceNodes = graph.BalanceNodes;

        // If the balance node has no edges, remove it
        if (!updatedBalanceNode.InEdges.Any() && !updatedBalanceNode.OutEdges.Any())
        {
            newNodes = newNodes.Remove(balanceNodeKey);
            newBalanceNodes = newBalanceNodes.Remove(balanceNodeKey);
        }
        else
        {
            newNodes = newNodes.SetItem(balanceNodeKey, updatedBalanceNode);
            newBalanceNodes = newBalanceNodes.SetItem(balanceNodeKey, updatedBalanceNode);
        }

        // If the avatar node has no edges, we may also remove it
        if (!updatedAvatarNode.InEdges.Any() && !updatedAvatarNode.OutEdges.Any())
        {
            newNodes = newNodes.Remove(address);
            newAvatarNodes = newAvatarNodes.Remove(address);
        }

        // Return the new graph
        return graph with
        {
            Edges = newEdges, Nodes = newNodes, AvatarNodes = newAvatarNodes, BalanceNodes = newBalanceNodes
        };
    }
}