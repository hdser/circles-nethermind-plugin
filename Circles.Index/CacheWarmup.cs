using Circles.Index.CirclesV2;
using Circles.Index.Common;
using Circles.Index.EventSourcing;
using Circles.Index.Query;
using Circles.Pathfinder.Data;
using Nethermind.Core;

namespace Circles.Index;

public static class CacheWarmup
{
    public static void InitCaches(Context<Aggregates> context)
    {
        context.Logger.Info("Caching Circles token addresses");
        CacheV1TokenAddresses(context);

        context.Logger.Info("Caching erc20 wrapper addresses");
        CacheErc20WrapperAddressees(context);

        context.Logger.Info("Loading v2 trust graph from trust events");
        ReplayV2TrustToAggregator(context);

        context.Logger.Info("Loading v2 balance graph from transfer events");
        ReplayV2TransfersToAggregator(context);
    }

    private static void CacheV1TokenAddresses(Context<Aggregates> context)
    {
        var selectSignups = new Select(
            "CrcV1",
            "Signup",
            ["token"],
            [],
            [],
            int.MaxValue,
            false,
            int.MaxValue);

        var sql = selectSignups.ToSql(context.Database);
        var result = context.Database.Select(sql);
        var rows = result.Rows.ToArray();

        context.Logger.Info($" * Found {rows.Length} Circles token addresses");

        foreach (var row in rows)
        {
            CirclesV1.LogParser.CirclesTokenAddresses.TryAdd(new Address(row[0]!.ToString()!), null);
        }

        context.Logger.Info("Caching Circles token addresses done");
    }

    private static void CacheErc20WrapperAddressees(Context<Aggregates> context)
    {
        var selectErc20WrapperDeployed = new Select(
            "CrcV2",
            "ERC20WrapperDeployed",
            ["erc20Wrapper"],
            [],
            [],
            int.MaxValue,
            false,
            int.MaxValue);

        var sql = selectErc20WrapperDeployed.ToSql(context.Database);
        var result = context.Database.Select(sql);
        object?[][] rows = result.Rows.ToArray();

        context.Logger.Info($" * Found {rows.Length} erc20 wrapper addresses");

        foreach (var row in rows)
        {
            LogParser.Erc20WrapperAddresses.TryAdd(new Address(row[0]!.ToString()!), null);
        }
    }

    private static void ReplayV2TransfersToAggregator(Context<Aggregates> context)
    {
        var transferEvents = new LoadGraph(context.Settings.IndexDbConnectionString)
            .LoadV2Transfers();
        
        TransferEvent? lastEvent = null;
        foreach (var transferEvent in transferEvents)
        {
            context.Aggregates.BalanceGraph.ProcessEvent(transferEvent);
            lastEvent = transferEvent;
        }
        
        context.Logger.Info($"Initialized v2 balance graph up to block {lastEvent?.BlockNumber}");
        
        var selectBlocksSinceLastTransferEvent = new Select(
            "System",
            "Block",
            ["blockNumber", "timestamp"],
            [new FilterPredicate("blockNumber", FilterType.GreaterThan, lastEvent?.BlockNumber ?? 0)],
            [],
            int.MaxValue,
            false,
            int.MaxValue);
        
        var sql = selectBlocksSinceLastTransferEvent.ToSql(context.Database);
        var result = context.Database.Select(sql);
        object?[][] rows = result.Rows.ToArray();
        long lastBlockNumber = 0;
        
        foreach (var row in rows)
        {
            var blockNumber = (long)row[0]!;
            lastBlockNumber = blockNumber;
            
            var timestamp = (long)row[1]!;
        
            var blockEvent = new BlockEvent(blockNumber, timestamp);
            context.Aggregates.BalanceGraph.ProcessEvent(blockEvent);
        }
        
        var balanceGraph = context.Aggregates.BalanceGraph.GetState();
        context.Logger.Info(
            $"Initialized balance graph. Nodes: {balanceGraph.Nodes.Count}, Edges: {balanceGraph.Edges.Count} up to block {lastBlockNumber}");
    }

    private static void ReplayV2TrustToAggregator(Context<Aggregates> context)
    {
        var trustEvents = new LoadGraph(context.Settings.IndexDbConnectionString)
            .LoadV2TrustEvents();

        Trust? lastEvent = null;
        foreach (var trustEvent in trustEvents)
        {
            context.Aggregates.TrustGraph.ProcessEvent(trustEvent);
            lastEvent = trustEvent;
        }

        context.Logger.Info($"Initialized v2 trust graph up to block {lastEvent?.BlockNumber}");

        var selectBlocksSinceLastTrustEvent = new Select(
            "System",
            "Block",
            ["blockNumber", "timestamp"],
            [new FilterPredicate("blockNumber", FilterType.GreaterThan, lastEvent?.BlockNumber ?? 0)],
            [],
            int.MaxValue,
            false,
            int.MaxValue);

        var sql = selectBlocksSinceLastTrustEvent.ToSql(context.Database);
        var result = context.Database.Select(sql);
        object?[][] rows = result.Rows.ToArray();

        foreach (var row in rows)
        {
            var blockNumber = (long)row[0]!;
            var timestamp = (long)row[1]!;

            context.Logger.Info($"Replaying block {blockNumber} to trust graph aggregator...");

            var blockEvent = new BlockEvent(blockNumber, timestamp);
            context.Aggregates.TrustGraph.ProcessEvent(blockEvent);
        }

        var trustGraph = context.Aggregates.TrustGraph.GetState();
        context.Logger.Info(
            $"Initialized trust graph. Nodes: {trustGraph.Nodes.Count}, Edges: {trustGraph.Edges.Count}");
    }
}