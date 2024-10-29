using Circles.Index.CirclesV2;
using Circles.Index.Common;
using Circles.Index.Query;
using Circles.Pathfinder.Graphs;
using Circles.Pathfinder.Edges;
using Nethermind.Int256;

namespace Circles.Pathfinder.Data
{
    /// <summary>
    /// Manages Circles trust relationships within an in-memory graph,
    /// providing functionalities to handle trust events and reorgs.
    /// </summary>
    /// <param name="finalityDepth">Specifies the number of blocks after which a block is considered final (no reorgs can occur).</param>
    public class InMemoryTrustGraph(int finalityDepth = 12)
    {
        public readonly TrustGraph TrustGraph = new();

        // Track edge actions by block number.
        private readonly Dictionary<long, List<TrustEdgeAction>> _edgeActionsByBlock = new();

        /// <summary>
        /// Initializes the graph up to a specific block from stored data.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="initialBlockNumber">The block number up to which data should be loaded.</param>
        public void InitializeFromDatabase(Context context, long initialBlockNumber)
        {
            // 1. Load all events from the database up to the initial block number
            var selectTrustEvents = new Select(
                "CrcV2",
                "Trust",
                ["blockNumber", "transactionIndex", "logIndex", "truster", "trustee", "expiryTime"],
                [new FilterPredicate("blockNumber", FilterType.LessThanOrEquals, initialBlockNumber)],
                [
                    new OrderBy("blockNumber", "asc"), new OrderBy("transactionIndex", "asc"),
                    new OrderBy("logIndex", "asc")
                ],
                int.MaxValue);

            var selectTrustEventsSql = selectTrustEvents.ToSql(context.Database);

            // 2. Process each event to build the graph
            var result = context.Database.Select(selectTrustEventsSql);
            var trustEvents = result.Rows.Select(o =>
            {
                var blockNumber = (long)(o[0] ?? throw new Exception("Block number is null"));
                var transactionIndex = (int)(o[1] ?? throw new Exception("Transaction index is null"));
                var logIndex = (int)(o[2] ?? throw new Exception("Log index is null"));
                var truster = (string)(o[3] ?? throw new Exception("Truster is null"));
                var trustee = (string)(o[4] ?? throw new Exception("Trustee is null"));
                var expiryTime = (string)(o[5] ?? throw new Exception("Expiry time is null"));

                return new Trust(blockNumber
                    , 0
                    , transactionIndex
                    , logIndex
                    , ""
                    , truster
                    , trustee
                    , UInt256.Parse(expiryTime));
            });

            foreach (var trustEvent in trustEvents)
            {
                HandleTrustEvent(trustEvent);
            }
        }

        /// <summary>
        /// Handles a reorg by undoing edge actions from the affected blocks onwards.
        /// </summary>
        /// <param name="fromBlock">The block number where the reorg starts.</param>
        public void HandleReorg(long fromBlock)
        {
            // Get the blocks to revert
            var blocksToRevert = _edgeActionsByBlock.Keys
                .Where(block => block >= fromBlock)
                .ToList();

            foreach (var block in blocksToRevert)
            {
                var actions = _edgeActionsByBlock[block];

                // Undo each edge action (in reverse order)
                foreach (var action in actions.AsEnumerable().Reverse())
                {
                    UndoEdgeAction(action);
                }

                // Remove the block's edge actions
                _edgeActionsByBlock.Remove(block);
            }
        }

        /// <summary>
        /// Processes a Trust event to immediately update the graph and record the action for potential reorgs.
        /// </summary>
        /// <param name="trust">The Trust event.</param>
        public void HandleTrustEvent(Trust trust)
        {
            // Determine if the Trust event represents an add or remove action.
            TrustEdgeAction edgeAction = IsTrustActive(trust)
                ? new AddTrustEdgeAction(trust.Truster, trust.Trustee)
                : new RemoveTrustEdgeAction(trust.Truster, trust.Trustee);

            // Apply the edge action immediately to the graph.
            ApplyEdgeAction(edgeAction);

            // Record the edge action for potential reorgs.
            if (!_edgeActionsByBlock.TryGetValue(trust.BlockNumber, out var actions))
            {
                actions = new List<TrustEdgeAction>();
                _edgeActionsByBlock[trust.BlockNumber] = actions;
            }

            actions.Add(edgeAction);

            // Optionally, remove finalized actions to save memory.
            var finalizedBlock = trust.BlockNumber - finalityDepth;
            if (_edgeActionsByBlock.ContainsKey(finalizedBlock))
            {
                _edgeActionsByBlock.Remove(finalizedBlock);
            }
        }

        /// <summary>
        /// Applies an edge action to the graph.
        /// </summary>
        private void ApplyEdgeAction(TrustEdgeAction edgeAction)
        {
            if (edgeAction is AddTrustEdgeAction)
            {
                TrustGraph.AddTrustEdge(edgeAction.Truster, edgeAction.Trustee);
            }
            else
            {
                RemoveEdgeFromGraph(edgeAction.Truster, edgeAction.Trustee);
            }
        }

        /// <summary>
        /// Undoes an edge action from the graph.
        /// </summary>
        private void UndoEdgeAction(TrustEdgeAction edgeAction)
        {
            if (edgeAction is AddTrustEdgeAction)
            {
                // Undo an AddTrustEdgeAction by removing the edge.
                RemoveEdgeFromGraph(edgeAction.Truster, edgeAction.Trustee);
            }
            else
            {
                // Undo a RemoveTrustEdgeAction by adding the edge back.
                TrustGraph.AddTrustEdge(edgeAction.Truster, edgeAction.Trustee);
            }
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
        /// Helper method to remove an edge from the graph and the respective nodes' in/out edges.
        /// </summary>
        private void RemoveEdgeFromGraph(string truster, string trustee)
        {
            var edge = new TrustEdge(truster, trustee);

            if (!TrustGraph.Edges.Remove(edge))
            {
                // Edge might not exist; that's acceptable in this context.
                return;
            }

            if (TrustGraph.AvatarNodes.TryGetValue(truster, out var trusterNode))
            {
                trusterNode.OutEdges.Remove(edge);
            }

            if (TrustGraph.AvatarNodes.TryGetValue(trustee, out var trusteeNode))
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
