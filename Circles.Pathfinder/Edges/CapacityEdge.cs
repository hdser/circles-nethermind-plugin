using System.Numerics;

namespace Circles.Pathfinder.Edges;

/// <summary>
/// Represents a capacity edge for potential token transfers between nodes.
/// </summary>
public record CapacityEdge(string From, string To, string Token, BigInteger InitialCapacity) : Edge(From, To);