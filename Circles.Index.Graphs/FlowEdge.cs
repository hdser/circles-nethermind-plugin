using System.Numerics;

namespace Circles.Index.Graphs;

/// <summary>
/// Represents a flow edge for actual token transfers between nodes.
/// </summary>
public record FlowEdge(string From, string To, string Token, BigInteger InitialCapacity)
    : CapacityEdge(From, To, Token, InitialCapacity)
{
    public BigInteger CurrentCapacity { get; init; } = InitialCapacity;
    public BigInteger Flow { get; init; } = BigInteger.Zero;
    public FlowEdge? ReverseEdge { get; init; }
}