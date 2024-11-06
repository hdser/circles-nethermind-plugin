using Circles.Pathfinder.Edges;

namespace Circles.Pathfinder.Nodes;

public abstract class Node
{
    public string Address { get; set; }
    public HashSet<Edge> OutEdges { get; } = new();
    public HashSet<Edge> InEdges { get; } = new();

    protected Node(string address)
    {
        Address = address;
    }
}