using System.Collections.Immutable;

namespace Circles.Index.Graphs;

public abstract record Node(string Address)
{
    public ImmutableHashSet<Edge> OutEdges { get; init; } = ImmutableHashSet<Edge>.Empty;
    public ImmutableHashSet<Edge> InEdges { get; init; } = ImmutableHashSet<Edge>.Empty;
}