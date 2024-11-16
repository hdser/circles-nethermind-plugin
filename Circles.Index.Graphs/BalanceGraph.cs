using System.Numerics;

namespace Circles.Index.Graphs;

public class BalanceGraph : IGraph<CapacityEdge>
{
    public IDictionary<string, Node> Nodes { get; } = new Dictionary<string, Node>();
    public HashSet<CapacityEdge> Edges { get; } = new();
    public IDictionary<string, BalanceNode> BalanceNodes { get; } = new Dictionary<string, BalanceNode>();
    public IDictionary<string, AvatarNode> AvatarNodes { get; } = new Dictionary<string, AvatarNode>();

    public void AddAvatar(string avatarAddress)
    {
        Nodes.Add(avatarAddress, new AvatarNode(avatarAddress));
    }

    /// <summary>
    /// Removes a node from the graph if it exists.
    /// </summary>
    /// <param name="key">The node's key</param>
    /// <returns>If the node existed and was removed.</returns>
    private bool TryRemoveNode(string key)
    {
        // 1. Check if the node exists
        if (!Nodes.TryGetValue(key, out var node))
        {
            return false;
        }

        // 2. Remove all edges connected to the node
        foreach (var edge in node.OutEdges.Cast<CapacityEdge>())
        {
            Edges.Remove(edge);
            Nodes[edge.To].InEdges.Remove(edge);
        }

        foreach (var edge in node.InEdges.Cast<CapacityEdge>())
        {
            Edges.Remove(edge);
            Nodes[edge.From].OutEdges.Remove(edge);
        }

        // 3. Remove the node
        BalanceNodes.Remove(key);
        AvatarNodes.Remove(key);

        return Nodes.Remove(key);
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

    public void SetBalance(string address, string token, BigInteger balance, long timestamp)
    {
        if (balance == BigInteger.Zero)
        {
            TryRemoveNode(address + "-" + token);

            if (AvatarNodes.TryGetValue(address, out var potentiallyDanglingAvatarNode)
                && !potentiallyDanglingAvatarNode.OutEdges.Any())
            {
                TryRemoveNode(address);
            }

            return;
        }

        if (!AvatarNodes.TryGetValue(address, out var avatarNode))
        {
            avatarNode = new AvatarNode(address);

            AvatarNodes.Add(address, avatarNode);
            Nodes.Add(address, avatarNode);
        }

        TryRemoveNode(address + "-" + token);

        var balanceNode = new BalanceNode(address, token, balance);
        balanceNode.LastChangeTimestamp = timestamp;

        Nodes.Add(balanceNode.Address, balanceNode);
        BalanceNodes.Add(balanceNode.Address, balanceNode);

        var capacityEdge = new CapacityEdge(avatarNode.Address, balanceNode.Address, token, balance);

        AvatarNodes[address].OutEdges.Add(capacityEdge);
        BalanceNodes[balanceNode.Address].InEdges.Add(capacityEdge);
        Edges.Add(capacityEdge);
    }
}