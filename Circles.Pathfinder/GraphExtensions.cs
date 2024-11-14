using System.Numerics;
using Circles.Index.Graphs;
using Google.OrTools.Graph;

namespace Circles.Pathfinder;

public static class GraphExtensions
{
    /// <summary>
    /// Computes the maximum flow from source to sink in the FlowGraph using Google's OR-Tools.
    /// Scales capacities to fit within Int64 range and scales flows back after computation.
    /// </summary>
    /// <param name="graph">The flow graph instance.</param>
    /// <param name="source">The source node identifier.</param>
    /// <param name="sink">The sink node identifier.</param>
    /// <param name="targetFlow">The desired flow value to reach.</param>
    /// <returns>The total flow value up to the target flow.</returns>
    public static BigInteger ComputeMaxFlowWithPaths(
        this FlowGraph graph,
        string source,
        string sink,
        BigInteger targetFlow)
    {
        // Map node addresses to indices
        var nodeIndices = new Dictionary<string, int>();
        int nodeIndex = 0;
        foreach (var node in graph.Nodes.Values)
        {
            nodeIndices[node.Address] = nodeIndex++;
        }

        // Find the maximum capacity to determine the scaling factor
        BigInteger maxCapacity = graph.Edges.Max(e => e.CurrentCapacity);

        // Determine the scaling factor to bring capacities within the Int64 range
        // We subtract a margin to prevent overflows
        BigInteger scalingFactor = (maxCapacity / long.MaxValue) + 1;

        // Handle the case where maxCapacity is already within long range
        if (scalingFactor < 1)
        {
            scalingFactor = 1;
        }

        // Create the MaxFlow solver
        var maxFlow = new MaxFlow();

        // Map edges to arc indices
        var edgeToArc = new Dictionary<FlowEdge, int>();

        // Add arcs (edges) to the solver
        foreach (var edge in graph.Edges)
        {
            int from = nodeIndices[edge.From];
            int to = nodeIndices[edge.To];

            // Scale down the capacity
            BigInteger scaledCapacity = edge.CurrentCapacity / scalingFactor;

            // Convert scaled capacity to long
            long capacity;
            if (scaledCapacity > long.MaxValue)
            {
                capacity = long.MaxValue;
            }
            else if (scaledCapacity < 0)
            {
                capacity = 0;
            }
            else
            {
                capacity = (long)scaledCapacity;
            }

            // Add the arc
            int arc = maxFlow.AddArcWithCapacity(from, to, capacity);

            // Store the mapping
            edgeToArc[edge] = arc;
        }

        // Set the source and sink indices
        int sourceIndex = nodeIndices[source];
        int sinkIndex = nodeIndices[sink];

        // Solve the max flow problem
        MaxFlow.Status status = maxFlow.Solve(sourceIndex, sinkIndex);

        if (status != MaxFlow.Status.OPTIMAL)
        {
            throw new Exception("Max flow could not find an optimal solution.");
        }

        // Get the maximum flow
        long maxFlowValue = maxFlow.OptimalFlow();

        // Scale the max flow back to the original units
        BigInteger resultFlow = new BigInteger(maxFlowValue) * scalingFactor;

        // Limit the flow to targetFlow if necessary
        if (resultFlow > targetFlow)
        {
            resultFlow = targetFlow;
            // Note: Adjusting individual flows proportionally may be complex.
            // For simplicity, we proceed with the computed flows.
        }

        // Update the flows in the edges
        foreach (var edge in graph.Edges)
        {
            int arc = edgeToArc[edge];
            long flow = maxFlow.Flow(arc);

            // Scale the flow back to the original units
            BigInteger scaledFlow = new BigInteger(flow) * scalingFactor;

            edge.Flow = scaledFlow;
            edge.CurrentCapacity -= scaledFlow;

            // Update reverse edge capacities if necessary
            if (edge.ReverseEdge != null)
            {
                edge.ReverseEdge.CurrentCapacity += scaledFlow;
            }
        }

        return resultFlow;
    }

    /// <summary>
    /// Finds the paths with positive flow in the FlowGraph and collects them.
    /// </summary>
    /// <param name="graph">The flow graph instance.</param>
    /// <param name="source">The source node identifier.</param>
    /// <param name="sink">The sink node identifier.</param>
    /// <param name="threshold">Only consider edges with flow greater than or equal to this threshold.</param>
    /// <returns>A list of paths with flow.</returns>
    public static List<List<FlowEdge>> ExtractPathsWithFlow(this FlowGraph graph, string source, string sink,
        BigInteger threshold)
    {
        var resultPaths = new List<List<FlowEdge>>();
        var visited = new HashSet<string>();

        // A helper method to perform DFS and collect paths with positive flow
        void Dfs(string currentNode, List<FlowEdge> currentPath)
        {
            if (currentNode == sink)
            {
                resultPaths.Add(new List<FlowEdge>(currentPath)); // Store a copy of the path
                return;
            }

            if (!graph.Nodes.TryGetValue(currentNode, out var node)) return;

            visited.Add(currentNode);

            foreach (var edge in node.OutEdges.OfType<FlowEdge>())
            {
                if (edge.Flow > 0 && !visited.Contains(edge.To))
                {
                    if (edge.Flow < threshold)
                    {
                        continue; // Skip edges with flow less than the threshold
                    }

                    currentPath.Add(edge); // Add edge to the current path
                    Dfs(edge.To, currentPath); // Recursively go deeper
                    currentPath.RemoveAt(currentPath.Count - 1); // Backtrack
                }
            }

            visited.Remove(currentNode);
        }

        // Start DFS from the source node
        Dfs(source, new List<FlowEdge>());

        return resultPaths;
    }
}