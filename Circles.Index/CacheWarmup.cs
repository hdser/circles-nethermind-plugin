using Circles.Index.CirclesV2;
using Circles.Index.Common;
using Circles.Index.Query;
using Circles.Pathfinder.Data;
using Circles.Pathfinder.EventSourcing;
using Nethermind.Core;

namespace Circles.Index;

public static class CacheWarmup
{
    public static void InitCaches(Context<TrustGraphAggregator> context)
    {
        context.Logger.Info("Caching Circles token addresses");
        CacheV1TokenAddresses(context);

        context.Logger.Info("Caching erc20 wrapper addresses");
        CacheErc20WrapperAddressess(context);

        context.Logger.Info("Loading v2 trust graph from trust events");
        ReplayV2TrustToAggregator(context);
    }

    private static void CacheV1TokenAddresses(Context<TrustGraphAggregator> context)
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

    private static void CacheErc20WrapperAddressess(Context<TrustGraphAggregator> context)
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

    private static void ReplayV2TrustToAggregator(Context<TrustGraphAggregator> context)
    {
        var trustEvents = new LoadGraph(context.Settings.IndexDbConnectionString)
            .LoadV2TrustEvents();

        Trust? lastEvent = null;
        foreach (var trustEvent in trustEvents)
        {
            context.Aggregates.ProcessEvent(trustEvent);
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

            context.Aggregates.ProcessEvent(new BlockEvent(blockNumber, timestamp));
        }

        context.Logger.Info(
            $"Initialized trust graph. Nodes: {context.Aggregates.GetState().Nodes.Count}, Edges: {context.Aggregates.GetState().Edges.Count}");
    }
}