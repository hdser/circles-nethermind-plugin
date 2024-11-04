using System.Numerics;
using Circles.Pathfinder.Data;
using Circles.Pathfinder.DTOs;
using Circles.Pathfinder.Edges;
using Circles.Pathfinder.Graphs;
using System.Diagnostics;

namespace Circles.Pathfinder;

public class V2Pathfinder : IPathfinder
{
    private readonly LoadGraph _loadGraph;
    private readonly GraphFactory _graphFactory;

    public V2Pathfinder(LoadGraph loadGraph, GraphFactory graphFactory)
    {
        _loadGraph = loadGraph;
        _graphFactory = graphFactory;
    }

    public async Task<MaxFlowResponse> ComputeMaxFlow(FlowRequest request)
    {
        if (string.IsNullOrEmpty(request.Source) || string.IsNullOrEmpty(request.Sink))
        {
            throw new ArgumentException("Source and Sink must be provided.");
        }

        if (!BigInteger.TryParse(request.TargetFlow, out var targetFlow))
        {
            throw new ArgumentException("TargetFlow must be a valid integer.");
        }

        var totalStopwatch = Stopwatch.StartNew();
        var stopwatch = new Stopwatch();

        // Load Trust and Balance Graphs
        stopwatch.Start();
        var trustGraph = _graphFactory.V2TrustGraph(_loadGraph);
        var balanceGraph = _graphFactory.V2BalanceGraph(_loadGraph);
        stopwatch.Stop();
        var loadGraphsTime = stopwatch.Elapsed;
        Console.WriteLine($"Time taken to load graphs: {loadGraphsTime.TotalMilliseconds} ms");

        // Create Capacity Graph
        stopwatch.Restart();
        var capacityGraph = _graphFactory.CreateCapacityGraph(balanceGraph, trustGraph);
        stopwatch.Stop();
        var createCapacityGraphTime = stopwatch.Elapsed;
        Console.WriteLine($"Time taken to create capacity graph: {createCapacityGraphTime.TotalMilliseconds} ms");

        // Create Flow Graph
        stopwatch.Restart();
        var flowGraph = _graphFactory.CreateFlowGraph(capacityGraph);
        stopwatch.Stop();
        var createFlowGraphTime = stopwatch.Elapsed;
        Console.WriteLine($"Time taken to create flow graph: {createFlowGraphTime.TotalMilliseconds} ms");

        // Validate Source and Sink
        stopwatch.Restart();
        if (!trustGraph.Nodes.ContainsKey(request.Source))
        {
            throw new ArgumentException($"Source node '{request.Source}' does not exist in the graph.");
        }

        if (!trustGraph.Nodes.ContainsKey(request.Sink))
        {
            throw new ArgumentException($"Sink node '{request.Sink}' does not exist in the graph.");
        }
        stopwatch.Stop();
        var validateSourceSinkTime = stopwatch.Elapsed;
        Console.WriteLine($"Time taken to validate source and sink: {validateSourceSinkTime.TotalMilliseconds} ms");

        // Compute Max Flow
        stopwatch.Restart();
        var maxFlow = flowGraph.ComputeMaxFlowWithPaths(request.Source, request.Sink, targetFlow);
        stopwatch.Stop();
        var computeMaxFlowTime = stopwatch.Elapsed;
        Console.WriteLine($"Time taken to compute max flow: {computeMaxFlowTime.TotalMilliseconds} ms");

        // Extract Paths with Flow
        stopwatch.Restart();
        var pathsWithFlow =
            flowGraph.ExtractPathsWithFlow(request.Source, request.Sink, BigInteger.Parse("0"));
        stopwatch.Stop();
        var extractPathsTime = stopwatch.Elapsed;
        Console.WriteLine($"Time taken to extract paths with flow: {extractPathsTime.TotalMilliseconds} ms");

        // Collapse balance nodes to get a collapsed graph
        stopwatch.Restart();
        var collapsedGraph = CollapseBalanceNodes(pathsWithFlow);
        stopwatch.Stop();
        var collapseBalanceNodesTime = stopwatch.Elapsed;
        Console.WriteLine($"Time taken to collapse balance nodes: {collapseBalanceNodesTime.TotalMilliseconds} ms");

        // Create transfer steps from the collapsed graph
        stopwatch.Restart();
        var transferSteps = new List<TransferPathStep>();

        foreach (var edge in collapsedGraph.Edges)
        {
            // For each edge, create a transfer step
            if (edge.Flow == BigInteger.Zero)
            {
                // Filter reverse edges
                continue;
            }

            transferSteps.Add(new TransferPathStep
            {
                From = edge.From,
                To = edge.To,
                TokenOwner = edge.Token,
                Value = edge.Flow.ToString()
            });
        }
        stopwatch.Stop();
        var createTransferStepsTime = stopwatch.Elapsed;
        Console.WriteLine($"Time taken to create transfer steps: {createTransferStepsTime.TotalMilliseconds} ms");

        // Prepare the response
        stopwatch.Restart();
        var response = new MaxFlowResponse(maxFlow.ToString(), transferSteps);
        stopwatch.Stop();
        var prepareResponseTime = stopwatch.Elapsed;
        Console.WriteLine($"Time taken to prepare response: {prepareResponseTime.TotalMilliseconds} ms");

        totalStopwatch.Stop();
        Console.WriteLine($"Total time taken: {totalStopwatch.Elapsed.TotalMilliseconds} ms");

        return response;
    }

    /// <summary>
    /// Collapses balance nodes in the paths and returns a collapsed flow graph.
    /// </summary>
    /// <param name="pathsWithFlow">The list of paths with flow.</param>
    /// <returns>A FlowGraph with balance nodes collapsed.</returns>
    private FlowGraph CollapseBalanceNodes(List<List<FlowEdge>> pathsWithFlow)
    {
        var collapsedGraph = new FlowGraph();

        // 1. Collect all avatar nodes
        var avatars = new HashSet<string>();
        pathsWithFlow.ForEach(o => o.ForEach(p =>
        {
            if (!IsBalanceNode(p.From))
            {
                avatars.Add(p.From);
            }

            if (!IsBalanceNode(p.To))
            {
                avatars.Add(p.To);
            }
        }));
        foreach (var avatar in avatars)
        {
            collapsedGraph.AddAvatar(avatar);
        }

        // 2. Remove all balance nodes, fuse the ends together, and add that edge to the new flow graph
        pathsWithFlow.ForEach(o =>
        {
            for (int i = 0; i < o.Count; i++)
            {
                var currentEdge = o[i];
                var nextEdge = i < o.Count - 1 ? o[i + 1] : null;

                if (IsBalanceNode(currentEdge.To) && nextEdge != null && nextEdge.From == currentEdge.To)
                {
                    // We are at a balance node, so we need to collapse it by merging currentEdge and nextEdge

                    // The flow through the balance node is limited by both the incoming and outgoing flows
                    var mergedFlow = BigInteger.Min(currentEdge.Flow, nextEdge.Flow);

                    var mergedEdge = new FlowEdge(
                        currentEdge.From,
                        nextEdge.To,
                        nextEdge.Token,
                        currentEdge.CurrentCapacity // Adjust as needed
                    )
                    {
                        Flow = mergedFlow,
                        ReverseEdge = nextEdge.ReverseEdge
                    };
                    try
                    {
                        collapsedGraph.AddFlowEdge(collapsedGraph, mergedEdge);
                        i++; // Skip the nextEdge since we have merged it
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);

                        // Log the stack trace
                        Console.WriteLine(e.StackTrace);

                        // Unpack the inner exception(s) recursively
                        while (e.InnerException != null)
                        {
                            e = e.InnerException;
                            Console.WriteLine(e.Message);
                            Console.WriteLine(e.StackTrace);
                        }
                    }
                }
                else
                {
                    try
                    {
                        // If not a balance node, add the current edge to the collapsed graph
                        collapsedGraph.AddFlowEdge(collapsedGraph, currentEdge);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);

                        // Log the stack trace
                        Console.WriteLine(e.StackTrace);

                        // Unpack the inner exception(s) recursively
                        while (e.InnerException != null)
                        {
                            e = e.InnerException;
                            Console.WriteLine(e.Message);
                            Console.WriteLine(e.StackTrace);
                        }
                    }
                }
            }
        });
        return collapsedGraph;
    }

    /// <summary>
    /// Determines if a given node address is a balance node.
    /// </summary>
    /// <param name="nodeAddress">The node address to check.</param>
    /// <returns>True if it's a balance node; otherwise, false.</returns>
    private bool IsBalanceNode(string nodeAddress)
    {
        return nodeAddress.Contains("-");
    }
}