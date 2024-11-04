using System.Numerics;
using Circles.Pathfinder.Data;
using System.Diagnostics;

namespace Circles.Pathfinder.Graphs
{
    public class GraphFactory
    {
        /// <summary>
        /// Loads all v2 trust edges from the database and creates a trust graph from them.
        /// </summary>
        /// <returns>A trust graph containing all v2 trust edges.</returns>
        public TrustGraph V2TrustGraph(LoadGraph loadGraph)
        {
            var stopwatch = Stopwatch.StartNew();

            var trustEdges = loadGraph.LoadV2Trust().ToArray();

            stopwatch.Stop();
            var loadTrustEdgesTime = stopwatch.Elapsed;
            Console.WriteLine($"Time taken to load V2Trust edges: {loadTrustEdgesTime.TotalMilliseconds} ms");

            stopwatch.Restart();

            var graph = new TrustGraph();
            foreach (var trustEdge in trustEdges)
            {
                if (!graph.AvatarNodes.ContainsKey(trustEdge.Truster))
                {
                    graph.AddAvatar(trustEdge.Truster);
                }

                if (!graph.AvatarNodes.ContainsKey(trustEdge.Trustee))
                {
                    graph.AddAvatar(trustEdge.Trustee);
                }

                graph.AddTrustEdge(trustEdge.Truster, trustEdge.Trustee);
            }

            stopwatch.Stop();
            var buildTrustGraphTime = stopwatch.Elapsed;
            Console.WriteLine($"Time taken to build TrustGraph: {buildTrustGraphTime.TotalMilliseconds} ms");

            return graph;
        }

        /// <summary>
        /// Loads all v2 balances from the database and creates a balance graph from them.
        /// </summary>
        /// <returns>A balance graph containing all v2 balances and holders.</returns>
        public BalanceGraph V2BalanceGraph(LoadGraph loadGraph)
        {
            var stopwatch = Stopwatch.StartNew();

            var balances = loadGraph.LoadV2Balances().ToArray();

            stopwatch.Stop();
            var loadBalancesTime = stopwatch.Elapsed;
            Console.WriteLine($"Time taken to load V2Balances: {loadBalancesTime.TotalMilliseconds} ms");

            stopwatch.Restart();

            var graph = new BalanceGraph();
            foreach (var balance in balances)
            {
                if (!graph.AvatarNodes.ContainsKey(balance.Account))
                {
                    graph.AddAvatar(balance.Account);
                }

                graph.AddBalance(balance.Account, balance.TokenAddress, BigInteger.Parse(balance.Balance));
            }

            stopwatch.Stop();
            var buildBalanceGraphTime = stopwatch.Elapsed;
            Console.WriteLine($"Time taken to build BalanceGraph: {buildBalanceGraphTime.TotalMilliseconds} ms");

            return graph;
        }

        /// <summary>
        /// Takes a balance graph and a trust graph and creates a capacity graph from them.
        /// </summary>
        /// <param name="balanceGraph">The balance graph to use.</param>
        /// <param name="trustGraph">The trust graph to use.</param>
        /// <returns>A capacity graph created from the balance and trust graphs.</returns>
        public CapacityGraph CreateCapacityGraph(BalanceGraph balanceGraph, TrustGraph trustGraph)
        {
            // Take the balance and trust graphs and create a capacity graph.
            // 1. Create a unified list of nodes from both graphs
            // 2. Leave the capacity edges from the balance graph in place
            // 3. Create more capacity edges based on the trust graph:
            //    - For each balance, check if there is a node that is willing to accept the balance (is trusting the token issuer)
            //    - If there is, create a capacity edge from the balance node to the accepting node
            
            var stopwatch = Stopwatch.StartNew();

            var capacityGraph = new CapacityGraph();

            // Step 1: Create a unified list of nodes from both graphs
            foreach (var avatar in balanceGraph.AvatarNodes.Values)
            {
                capacityGraph.AddAvatar(avatar.Address);
            }

            foreach (var avatar in trustGraph.AvatarNodes.Values)
            {
                capacityGraph.AddAvatar(avatar.Address);
            }

            // Add BalanceNodes
            foreach (var balanceNode in balanceGraph.BalanceNodes.Values)
            {
                capacityGraph.AddBalanceNode(balanceNode.Address, balanceNode.Token, balanceNode.Amount);
            }

            // Step 2: Leave the capacity edges from the balance graph in place
            foreach (var capacityEdge in balanceGraph.Edges)
            {
                capacityGraph.AddCapacityEdge(
                    capacityEdge.From,
                    capacityEdge.To,
                    capacityEdge.Token,
                    capacityEdge.InitialCapacity
                );
            }

            stopwatch.Stop();
            var initialCapacityGraphTime = stopwatch.Elapsed;
            Console.WriteLine($"Time taken to initialize CapacityGraph: {initialCapacityGraphTime.TotalMilliseconds} ms");

            stopwatch.Restart();

            // Step 3: Create more capacity edges based on the trust graph
            // Optimization: Precompute a trustee-to-trusters lookup dictionary
            var trusteeToTrusters = new Dictionary<string, List<string>>();

            foreach (var edge in trustGraph.Edges)
            {
                if (!trusteeToTrusters.TryGetValue(edge.To, out var trusters))
                {
                    trusters = new List<string>();
                    trusteeToTrusters[edge.To] = trusters;
                }

                trusters.Add(edge.From);
            }

            foreach (var balanceNode in balanceGraph.BalanceNodes.Values)
            {
                string tokenIssuer = balanceNode.Token;

                if (trusteeToTrusters.TryGetValue(tokenIssuer, out var acceptingNodes))
                {
                    foreach (var acceptingNode in acceptingNodes)
                    {
                        // Avoid creating edges to self or invalid nodes
                        if (acceptingNode == balanceNode.HolderAddress)
                            continue;

                        capacityGraph.AddCapacityEdge(
                            balanceNode.Address,
                            acceptingNode,
                            balanceNode.Token,
                            balanceNode.Amount
                        );
                    }
                }
            }

            stopwatch.Stop();
            var buildCapacityGraphTime = stopwatch.Elapsed;
            Console.WriteLine($"Time taken to build CapacityGraph: {buildCapacityGraphTime.TotalMilliseconds} ms");

            return capacityGraph;
        }

        public FlowGraph CreateFlowGraph(CapacityGraph capacityGraph)
        {
            var stopwatch = Stopwatch.StartNew();

            var flowGraph = new FlowGraph();
            flowGraph.AddCapacity(capacityGraph);

            stopwatch.Stop();
            var buildFlowGraphTime = stopwatch.Elapsed;
            Console.WriteLine($"Time taken to build FlowGraph: {buildFlowGraphTime.TotalMilliseconds} ms");

            return flowGraph;
        }
    }
}
