using Circles.Index.Data.Cache;
using Circles.Index.Data.Model;
using Circles.Index.Data.Sqlite;
using Microsoft.Data.Sqlite;
using Nethermind.Logging;

namespace Circles.Index.Indexer;

public static class ReorgHandler
{
    private record ReorgAffectedData(CirclesSignupDto[] Signups, CirclesTrustDto[] Trusts,
        CirclesHubTransferDto[] HubTransfers, CirclesTransferDto[] Transfers);

    public static async Task ReorgAt(SqliteConnection connection, MemoryCache cache, ILogger logger, long block)
    {
        ReorgAffectedData affectedData = await GetAffectedItems(connection, block);
        logger.Info($"Deleting all blocks greater or equal {block} from the index ..");
        logger.Info($"Affected signups: {affectedData.Signups.Length}");
        logger.Info($"Affected trusts: {affectedData.Trusts.Length}");
        logger.Info($"Affected hub transfers: {affectedData.HubTransfers.Length}");
        logger.Info($"Affected crc transfers: {affectedData.Transfers.Length}");

        DeleteFromBlockOnwards(connection, block);
        await MaintainCache(cache, connection, affectedData);
    }

    private static async Task MaintainCache(MemoryCache cache, SqliteConnection connection, ReorgAffectedData affectedData)
    {
        foreach (CirclesSignupDto signup in affectedData.Signups)
        {
            cache.RemoveUser(signup);
        }

        foreach (CirclesTrustDto trust in affectedData.Trusts)
        {
            cache.RemoveTrustRelation(trust);
        }

        Task<CirclesTrustDto[]>[] trustCacheRefreshData = affectedData.Trusts
            .Select(t => new CirclesTrustQuery { CanSendToAddress = t.UserAddress })
            .Select(tq => Task.Run(() => Query.CirclesTrusts(connection, tq, int.MaxValue).ToArray()))
            .ToArray();

        await Task.WhenAll(trustCacheRefreshData);

        foreach (Task<CirclesTrustDto[]> userTrusts in trustCacheRefreshData)
        {
            foreach (CirclesTrustDto trust in userTrusts.Result)
            {
                cache.TrustGraph.AddOrUpdateEdge(trust.UserAddress, trust.CanSendToAddress, trust.Limit);
            }
        }

        HashSet<string> affectedTokens = new();
        HashSet<string> affectedUsers = new();

        foreach (CirclesTransferDto affectedTransfer in affectedData.Transfers)
        {
            affectedTokens.Add(affectedTransfer.TokenAddress);
            affectedUsers.Add(affectedTransfer.FromAddress);
            affectedUsers.Add(affectedTransfer.ToAddress);
        }

        // Remove all balances of affected users that concern the affected tokens
        foreach (string affectedUser in affectedUsers)
        {
            foreach (string affectedToken in affectedTokens)
            {
                cache.Balances.Remove(affectedUser, affectedToken);
            }
        }

        // Rebuild the cache for all affected tokens
        foreach (string affectedToken in affectedTokens)
        {
            IEnumerable<CirclesTransferDto> tokenTransfers = Query.CirclesTransfers(connection, new CirclesTransferQuery
            {
                TokenAddress = affectedToken,
                SortOrder = SortOrder.Ascending
            }, int.MaxValue);

            foreach (CirclesTransferDto transfer in tokenTransfers)
            {
                if (transfer.FromAddress != StateMachine._zeroAddress)
                {
                    cache.Balances.Out(transfer.FromAddress, transfer.TokenAddress, transfer.Amount);
                }
                cache.Balances.In(transfer.ToAddress, transfer.TokenAddress, transfer.Amount);
            }
        }
    }

    /// <summary>
    /// Deletes all data from the specified block number onwards.
    /// </summary>
    /// <param name="connection">The connection to the database</param>
    /// <param name="reorgAt">The block number to delete from (inclusive)</param>
    public static void DeleteFromBlockOnwards(SqliteConnection connection, long reorgAt)
    {
        using SqliteTransaction transaction = connection.BeginTransaction();
        try
        {
            using SqliteCommand deleteRelevantBlocksCmd = connection.CreateCommand();
            deleteRelevantBlocksCmd.CommandText = @$"
                DELETE FROM {TableNames.BlockRelevant}
                WHERE block_number >= @reorgAt;
            ";
            deleteRelevantBlocksCmd.Transaction = transaction;
            deleteRelevantBlocksCmd.Parameters.AddWithValue("@reorgAt", reorgAt);
            deleteRelevantBlocksCmd.ExecuteNonQuery();

            using SqliteCommand deleteIrrelevantBlocksCmd = connection.CreateCommand();
            deleteIrrelevantBlocksCmd.CommandText = @$"
                DELETE FROM {TableNames.BlockIrrelevant}
                WHERE block_number >= @reorgAt;
            ";
            deleteIrrelevantBlocksCmd.Transaction = transaction;
            deleteIrrelevantBlocksCmd.Parameters.AddWithValue("@reorgAt", reorgAt);
            deleteIrrelevantBlocksCmd.ExecuteNonQuery();

            using SqliteCommand deleteCirclesSignupCmd = connection.CreateCommand();
            deleteCirclesSignupCmd.CommandText = @$"
                DELETE FROM {TableNames.CirclesSignup}
                WHERE block_number >= @reorgAt;
            ";
            deleteCirclesSignupCmd.Transaction = transaction;
            deleteCirclesSignupCmd.Parameters.AddWithValue("@reorgAt", reorgAt);
            deleteCirclesSignupCmd.ExecuteNonQuery();

            using SqliteCommand deleteCirclesTrustCmd = connection.CreateCommand();
            deleteCirclesTrustCmd.CommandText = @$"
                DELETE FROM {TableNames.CirclesTrust}
                WHERE block_number >= @reorgAt;
            ";
            deleteCirclesTrustCmd.Transaction = transaction;
            deleteCirclesTrustCmd.Parameters.AddWithValue("@reorgAt", reorgAt);
            deleteCirclesTrustCmd.ExecuteNonQuery();

            using SqliteCommand deleteCirclesHubTransferCmd = connection.CreateCommand();
            deleteCirclesHubTransferCmd.CommandText = @$"
                DELETE FROM {TableNames.CirclesHubTransfer}
                WHERE block_number >= @reorgAt;
            ";
            deleteCirclesHubTransferCmd.Transaction = transaction;
            deleteCirclesHubTransferCmd.Parameters.AddWithValue("@reorgAt", reorgAt);
            deleteCirclesHubTransferCmd.ExecuteNonQuery();

            using SqliteCommand deleteCirclesTransferCmd = connection.CreateCommand();
            deleteCirclesTransferCmd.CommandText = @$"
                DELETE FROM {TableNames.CirclesTransfer}
                WHERE block_number >= @reorgAt;
            ";
            deleteCirclesTransferCmd.Transaction = transaction;
            deleteCirclesTransferCmd.Parameters.AddWithValue("@reorgAt", reorgAt);
            deleteCirclesTransferCmd.ExecuteNonQuery();

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }

    private static async Task<ReorgAffectedData> GetAffectedItems(SqliteConnection connection, long reorgAt)
    {
        CirclesSignupQuery affectedSignupQuery = new() { BlockNumberRange = { Min = reorgAt } };
        Task<CirclesSignupDto[]> affectedSignups =
            Task.Run(() => Query.CirclesSignups(connection, affectedSignupQuery, int.MaxValue).ToArray());

        CirclesTrustQuery affectedTrustQuery = new() { BlockNumberRange = { Min = reorgAt } };
        Task<CirclesTrustDto[]> affectedTrusts =
            Task.Run(() => Query.CirclesTrusts(connection, affectedTrustQuery, int.MaxValue).ToArray());

        CirclesHubTransferQuery affectedHubTransferQuery = new() { BlockNumberRange = { Min = reorgAt } };
        Task<CirclesHubTransferDto[]> affectedHubTransfers =
            Task.Run(() => Query.CirclesHubTransfers(connection, affectedHubTransferQuery, int.MaxValue).ToArray());

        CirclesTransferQuery affectedTransferQuery = new() { BlockNumberRange = { Min = reorgAt } };
        Task<CirclesTransferDto[]> affectedTransfers =
            Task.Run(() => Query.CirclesTransfers(connection, affectedTransferQuery, int.MaxValue).ToArray());

        await Task.WhenAll(
            affectedSignups,
            affectedTrusts,
            affectedHubTransfers,
            affectedTransfers
        );

        return new ReorgAffectedData(
            Signups: affectedSignups.Result,
            Trusts: affectedTrusts.Result,
            HubTransfers: affectedHubTransfers.Result,
            Transfers: affectedTransfers.Result);
    }
}
