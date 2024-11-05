using Circles.Pathfinder.Edges;
using Circles.Pathfinder.Nodes;

namespace Circles.Pathfinder.Graphs;

public class TrustGraph : IGraph<TrustEdge>
{
    public IDictionary<string, Node> Nodes { get; } = new Dictionary<string, Node>();
    public IDictionary<string, AvatarNode> AvatarNodes { get; } = new Dictionary<string, AvatarNode>();
    public HashSet<TrustEdge> Edges { get; } = new();

    public void AddAvatar(string avatarAddress)
    {
        var avatar = new AvatarNode(avatarAddress);
        AvatarNodes.Add(avatarAddress, avatar);
        Nodes.Add(avatarAddress, avatar);
    }

    public void AddTrustEdge(string truster, string trustee)
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

        var trustEdge = new TrustEdge(truster, trustee);
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