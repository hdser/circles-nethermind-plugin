using Circles.Index.CirclesV2;
using Circles.Index.Common;
using Circles.Index.Query;
using Circles.Pathfinder.Graphs;
using Circles.Pathfinder.Edges;

namespace Circles.Pathfinder.Data
{
    /// <summary>
    /// Manages Circles trust relationships within an in-memory graph,
    /// providing functionalities to handle trust events and reorgs.
    /// </summary>
    /// <param name="finalityDepth">Specifies the number of blocks after which a block is considered final (no reorgs can occur).</param>
    public class InMemoryTrustGraph(int finalityDepth = 12)
    {
        private readonly TrustGraph _trustGraph = new();

        // Track pending edge actions by block number.
        private readonly Dictionary<long, List<TrustEdgeAction>> _pendingEdgeActions = new();

        /// <summary>
        /// Initializes the graph up to a specific block from stored data.
        /// </summary>
        /// <param name="initialBlockNumber">The block number up to which data should be loaded.</param>
        public void InitializeFromDatabase(Context context, long initialBlockNumber)
        {
            // TODO: 1. Load all events from the database up to the initial block number
            /*
             select "blockNumber",
                      timestamp,
                      "transactionIndex",
                      "logIndex",
                      "transactionHash",
                      truster,
                      trustee,
                      "expiryTime"
               from "CrcV2_Trust"
               order by "blockNumber", "transactionIndex", "logIndex";
             */
            var selectTrustEvents = new Select(
                "CrcV2",
                "Trust",
                ["blockNumber", "transactionIndex", "logIndex", "truster", "trustee", "expiryTime"],
                [new FilterPredicate("token", FilterType.In, allTokenAddressStrings)],
                [new OrderBy("blockNumber", "asc"), new OrderBy("transactionIndex"), new OrderBy("logIndex")],
                int.MaxValue);

            
            // TODO: 2. Process each event to build the graph
            
            context.Database.Select()
            
            throw new NotImplementedException();
        }

        /// <summary>
        /// Handles a reorg by removing pending edge actions from the affected blocks onwards.
        /// </summary>
        /// <param name="fromBlock">The block number where the reorg starts.</param>
        public void HandleReorg(long fromBlock)
        {
            // Remove pending edge actions (not yet applied to the graph)
            var blocksToRemove = new List<long>();
            foreach (var block in _pendingEdgeActions.Keys)
            {
                if (block >= fromBlock)
                {
                    blocksToRemove.Add(block);
                }
            }

            foreach (var block in blocksToRemove)
            {
                _pendingEdgeActions.Remove(block);
            }
        }

        /// <summary>
        /// Processes a Trust event to either add or remove a tentative edge.
        /// Finalizes edge actions that have reached the finality depth.
        /// </summary>
        /// <param name="trust">The Trust event.</param>
        public void HandleTrustEvent(Trust trust)
        {
            // Determine if the Trust event represents an add or remove action.
            TrustEdgeAction edgeAction = IsTrustActive(trust)
                ? new AddTrustEdgeAction(trust.Truster, trust.Trustee)
                : new RemoveTrustEdgeAction(trust.Truster, trust.Trustee);

            // Add the edge action to pending actions for delayed finality.
            if (!_pendingEdgeActions.TryGetValue(trust.BlockNumber, out var actions))
            {
                actions = new List<TrustEdgeAction>();
                _pendingEdgeActions[trust.BlockNumber] = actions;
            }

            actions.Add(edgeAction);

            // Finalize any pending actions that have reached the finality depth.
            FinalizeEdgeActions(trust.BlockNumber);
        }

        /// <summary>
        /// Determines if a Trust event represents an active trust relationship.
        /// </summary>
        private bool IsTrustActive(Trust trust)
        {
            // Implement logic to determine if the trust is active.
            // For example, compare ExpiryTime with current time.
            return trust.ExpiryTime > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>
        /// Finalizes edge actions that have reached the finality depth.
        /// </summary>
        /// <param name="currentBlockNumber">The current block number from the Trust event.</param>
        private void FinalizeEdgeActions(long currentBlockNumber)
        {
            var finalizableBlock = currentBlockNumber - finalityDepth;

            if (_pendingEdgeActions.TryGetValue(finalizableBlock, out var actionsToFinalize))
            {
                foreach (var action in actionsToFinalize)
                {
                    if (action is AddTrustEdgeAction)
                    {
                        _trustGraph.AddTrustEdge(action.Truster, action.Trustee);
                    }
                    else
                    {
                        RemoveEdgeFromGraph(action.Truster, action.Trustee);
                    }
                }

                // Remove finalized actions from pending as they are now part of the finalized graph.
                _pendingEdgeActions.Remove(finalizableBlock);
            }
        }

        /// <summary>
        /// Helper method to remove an edge from the graph and the respective nodes' in/out edges.
        /// </summary>
        private void RemoveEdgeFromGraph(string truster, string trustee)
        {
            var edge = new TrustEdge(truster, trustee);

            if (!_trustGraph.Edges.Remove(edge))
            {
                throw new ArgumentException($"Edge {truster} -> {trustee} not found in the graph.");
            }

            if (_trustGraph.AvatarNodes.TryGetValue(truster, out var trusterNode))
            {
                trusterNode.OutEdges.Remove(edge);
            }

            if (_trustGraph.AvatarNodes.TryGetValue(trustee, out var trusteeNode))
            {
                trusteeNode.InEdges.Remove(edge);
            }
        }
    }

    /// <summary>
    /// Represents an action on an edge (add or remove).
    /// </summary>
    public abstract class TrustEdgeAction(string truster, string trustee)
    {
        public string Truster { get; } = truster.ToLower();
        public string Trustee { get; } = trustee.ToLower();
    }

    public class AddTrustEdgeAction(string truster, string trustee) : TrustEdgeAction(truster, trustee);

    public class RemoveTrustEdgeAction(string truster, string trustee) : TrustEdgeAction(truster, trustee);
}