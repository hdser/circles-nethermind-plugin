using Circles.Index.CirclesV2;
using Circles.Pathfinder.Graphs;
using Circles.Pathfinder.Edges;
using Nethermind.Int256;
using Npgsql;

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

        public async Task InitializeFromDatabase(string connectionString, long initialBlockNumber)
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // 1. Load all events from the database up to the initial block number
            var sql = @"
                select ""blockNumber"",
                       ""transactionIndex"",
                       ""transactionHash"",
                       ""logIndex"",
                       ""canSendTo"",
                       ""user"",
                       ""limit""
                from ""CrcV1_Trust""
                where ""blockNumber"" <= @initialBlockNumber
                order by ""blockNumber"", ""transactionIndex"", ""logIndex"";";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("initialBlockNumber", initialBlockNumber);

            await using var reader = await command.ExecuteReaderAsync();
            while (reader.Read())
            {
                var blockNumber = reader.GetInt64(0);
                var transactionIndex = reader.GetInt32(1);
                var transactionHash = reader.GetString(2);
                var logIndex = reader.GetInt32(3);
                var trustee = reader.GetString(4);
                var truster = reader.GetString(5);
                var expiryTime = reader.GetInt64(6) > 0 ? "2530771325" : "0";

                var trust = new Trust(blockNumber
                    , 0
                    , transactionIndex
                    , logIndex
                    , transactionHash
                    , truster
                    , trustee
                    , UInt256.Parse(expiryTime));

                HandleTrustEvent(trust);
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

        private long lastBlockNumber = 0;
        
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
            var finalizedBlocks = _edgeActionsByBlock.Where(o => o.Key <= trust.BlockNumber - finalityDepth).ToList();
            foreach (var finalizedBlock in finalizedBlocks)
            {
                _edgeActionsByBlock.Remove(finalizedBlock.Key);
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
                throw new Exception("Edge not found in graph.");
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