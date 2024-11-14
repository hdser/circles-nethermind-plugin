namespace Circles.Index.Graphs;

public interface IGraph<TEdge>
    where TEdge : Edge
{
    IDictionary<string, Node> Nodes { get; }
    HashSet<TEdge> Edges { get; }
}