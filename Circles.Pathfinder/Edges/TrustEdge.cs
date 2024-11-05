namespace Circles.Pathfinder.Edges;

/// <summary>
/// Represents a trust relationship between two nodes.
/// </summary>
public record TrustEdge(string From, string To) : Edge(From, To);