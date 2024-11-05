using System.Numerics;

namespace Circles.Pathfinder.Edges;

/// <summary>
/// Represents a flow edge for actual token transfers between nodes.
/// </summary>
public record FlowEdge(string From, string To, string Token, BigInteger InitialCapacity)
    : CapacityEdge(From, To, Token, InitialCapacity)
{
    public BigInteger CurrentCapacity { get; set; } = InitialCapacity;
    public BigInteger Flow { get; set; } = 0;
    public FlowEdge? ReverseEdge { get; set; }
}