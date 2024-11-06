using Nethermind.Int256;

namespace Circles.Pathfinder.Edges;

/// <summary>
/// Represents a trust relationship between two nodes.
/// </summary>
public record TrustEdge(string From, string To, UInt256 ExpiryTime) : Edge(From, To);