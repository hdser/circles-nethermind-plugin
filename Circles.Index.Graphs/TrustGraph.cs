using Nethermind.Int256;

namespace Circles.Index.Graphs;

public class TrustGraph : IGraph<TrustEdge>
{
    public IDictionary<string, Node> Nodes { get; } = new Dictionary<string, Node>();
    public IDictionary<string, AvatarNode> AvatarNodes { get; } = new Dictionary<string, AvatarNode>();
    public HashSet<TrustEdge> Edges { get; } = new();

    public AvatarNode AddAvatar(string avatarAddress)
    {
        var avatar = new AvatarNode(avatarAddress);
        AvatarNodes.Add(avatarAddress, avatar);
        Nodes.Add(avatarAddress, avatar);

        return avatar;
    }

    public void RemoveAvatar(string avatarAddress)
    {
        if (!AvatarNodes.TryGetValue(avatarAddress, out var avatarNode))
        {
            throw new Exception("Avatar not found in graph.");
        }

        foreach (var edge in avatarNode.InEdges.Cast<TrustEdge>())
        {
            Edges.Remove(edge);
            AvatarNodes[edge.From].OutEdges.Remove(edge);
        }

        foreach (var edge in avatarNode.OutEdges.Cast<TrustEdge>())
        {
            Edges.Remove(edge);
            AvatarNodes[edge.To].InEdges.Remove(edge);
        }

        AvatarNodes.Remove(avatarAddress);
        Nodes.Remove(avatarAddress);
    }

    public void AddTrustEdge(string truster, string trustee, UInt256 expiryTime)
    {
        truster = truster.ToLower();
        trustee = trustee.ToLower();

        if (!AvatarNodes.TryGetValue(truster, out var trusterNode))
        {
            trusterNode = new AvatarNode(truster);
            AvatarNodes[truster] = trusterNode;
        }

        if (!AvatarNodes.TryGetValue(trustee, out var trusteeNode))
        {
            trusteeNode = new AvatarNode(trustee);
            AvatarNodes[trustee] = trusteeNode;
        }

        var trustEdge = new TrustEdge(truster, trustee, expiryTime);
        if (!trusterNode.OutEdges.Contains(trustEdge))
        {
            trusterNode.OutEdges.Add(trustEdge);
        }

        if (!trusteeNode.InEdges.Contains(trustEdge))
        {
            trusteeNode.InEdges.Add(trustEdge);
        }

        Edges.Add(trustEdge);
    }
}