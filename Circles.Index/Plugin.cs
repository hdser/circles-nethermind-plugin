﻿using Circles.Index.Common;
using Circles.Index.Postgres;
using Circles.Index.Query;
using Circles.Index.Rpc;
using Nethermind.Api;
using Nethermind.Api.Extensions;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.JsonRpc.Modules;
using Nethermind.Logging;
using Nethermind.Synchronization.ParallelSync;
using Npgsql;

namespace Circles.Index;

public class Plugin : INethermindPlugin
{
    public string Name => "Circles";

    public string Description =>
        "Indexes Circles related events and provides query capabilities via JSON-RPC.";

    public string Author => "Gnosis";

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private StateMachine? _indexerMachine;
    private Context? _indexerContext;
    private int _isProcessing;
    private int _newItemsArrived;
    private long _latestHeadToIndex = -1;

    public async Task Init(INethermindApi nethermindApi)
    {
        IDatabaseSchema common = new Common.DatabaseSchema();
        IDatabaseSchema v1 = new CirclesV1.DatabaseSchema();
        IDatabaseSchema v2 = new CirclesV2.DatabaseSchema();
        IDatabaseSchema v2NameRegistry = new CirclesV2.NameRegistry.DatabaseSchema();
        IDatabaseSchema v2StandardTreasury = new CirclesV2.StandardTreasury.DatabaseSchema();
        IDatabaseSchema circlesViews = new CirclesViews.DatabaseSchema();
        IDatabaseSchema databaseSchema =
            new CompositeDatabaseSchema([common, v1, v2, v2NameRegistry, v2StandardTreasury, circlesViews]);

        ILogger baseLogger = nethermindApi.LogManager.GetClassLogger();
        InterfaceLogger pluginLogger = new LoggerWithPrefix($"{Name}: ", baseLogger);

        Settings settings = new();
        IDatabase database = new PostgresDb(settings.IndexDbConnectionString, databaseSchema);

        LogSettings(pluginLogger, settings, database);
        database.Migrate();

        Sink sink = new Sink(
            database,
            new CompositeSchemaPropertyMap([
                v1.SchemaPropertyMap, v2.SchemaPropertyMap, v2NameRegistry.SchemaPropertyMap,
                v2StandardTreasury.SchemaPropertyMap
            ]),
            new CompositeEventDtoTableMap([
                v1.EventDtoTableMap, v2.EventDtoTableMap, v2NameRegistry.EventDtoTableMap,
                v2StandardTreasury.EventDtoTableMap
            ]),
            settings.EventBufferSize);

        InitCaches(pluginLogger, database);

        ILogParser[] logParsers =
        [
            new CirclesV1.LogParser(settings.CirclesV1HubAddress),
            new CirclesV2.LogParser(settings.CirclesV2HubAddress, settings.CirclesErc20LiftAddress),
            new CirclesV2.NameRegistry.LogParser(settings.CirclesNameRegistryAddress),
            new CirclesV2.StandardTreasury.LogParser(settings.CirclesStandardTreasuryAddress)
        ];

        _indexerContext = new Context(
            nethermindApi,
            pluginLogger,
            settings,
            database,
            logParsers,
            sink);

        _indexerMachine = new StateMachine(
            _indexerContext
            , nethermindApi.BlockTree!
            , nethermindApi.ReceiptFinder!
            , _cancellationTokenSource.Token);

        await _indexerMachine.TransitionTo(StateMachine.State.Initial);

        nethermindApi.BlockTree!.NewHeadBlock += (_, args) =>
        {
            var fullSyncInfo = nethermindApi.EthSyncingInfo?.GetFullInfo();

            if (fullSyncInfo?.IsSyncing ?? true)
            {
                switch (fullSyncInfo?.SyncMode)
                {
                    // Should handle blocks in the following sync modes:
                    case SyncMode.Full:
                    case SyncMode.DbLoad:
                        break;
                    default:
                        return;
                }
            }

            Interlocked.Exchange(ref _newItemsArrived, 1);
            Interlocked.Exchange(ref _latestHeadToIndex, args.Block.Number);

            HandleNewHead();
        };
    }

    private void LogSettings(InterfaceLogger pluginLogger, Settings settings, IDatabase database)
    {
        // Log all indexed events
        pluginLogger.Info("Indexing events:");
        foreach (var databaseSchemaTable in database.Schema.Tables)
        {
            pluginLogger.Info(
                $" * Topic: {databaseSchemaTable.Value.Topic.ToHexString()}; Name: {databaseSchemaTable.Key.Namespace}_{databaseSchemaTable.Key.Table}");
        }

        NpgsqlConnectionStringBuilder connectionStringBuilder = new(settings.IndexDbConnectionString);
        pluginLogger.Info("Index database: " + connectionStringBuilder.Database);
        pluginLogger.Info(" * host: " + connectionStringBuilder.Host);
        pluginLogger.Info(" * port: " + connectionStringBuilder.Port);
        pluginLogger.Info(" * user: " + connectionStringBuilder.Username);

        pluginLogger.Info("Contract addresses: ");
        pluginLogger.Info(" * V1 Hub address: " + settings.CirclesV1HubAddress);
        pluginLogger.Info(" * V2 Hub address: " + settings.CirclesV2HubAddress);
        pluginLogger.Info(" * V2 Name Registry address: " + settings.CirclesNameRegistryAddress);
        pluginLogger.Info(" * V2 Standard Treasury address: " + settings.CirclesStandardTreasuryAddress);
        // pluginLogger.Info("Start index from: " + settings.StartBlock);
    }

    private static void InitCaches(InterfaceLogger logger, IDatabase database)
    {
        logger.Info("Caching Circles token addresses");

        var selectSignups = new Select(
            "CrcV1",
            "Signup",
            ["token"],
            [],
            [],
            int.MaxValue,
            false,
            int.MaxValue);

        var sql = selectSignups.ToSql(database);
        var result = database.Select(sql);
        var rows = result.Rows.ToArray();

        logger.Info($" * Found {rows.Length} Circles token addresses");

        foreach (var row in rows)
        {
            CirclesV1.LogParser.CirclesTokenAddresses.TryAdd(new Address(row[0]!.ToString()!), null);
        }

        logger.Info("Caching Circles token addresses done");

        logger.Info("Caching erc20 wrapper addresses");

        var selectErc20WrapperDeployed = new Select(
            "CrcV2",
            "ERC20WrapperDeployed",
            ["erc20Wrapper"],
            [],
            [],
            int.MaxValue,
            false,
            int.MaxValue);

        sql = selectErc20WrapperDeployed.ToSql(database);
        result = database.Select(sql);
        rows = result.Rows.ToArray();

        logger.Info($" * Found {rows.Length} erc20 wrapper addresses");

        foreach (var row in rows)
        {
            CirclesV2.LogParser.Erc20WrapperAddresses.TryAdd(new Address(row[0]!.ToString()!), null);
        }
    }

    private void HandleNewHead()
    {
        // Start the processing task if not already running
        if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0)
        {
            Task.Run(ProcessBlocksAsync, _cancellationTokenSource.Token);
        }
    }

    private async Task ProcessBlocksAsync()
    {
        try
        {
            do
            {
                _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                long toIndex = Interlocked.Exchange(ref _latestHeadToIndex, -1);
                if (toIndex == -1)
                {
                    continue;
                }

                await _indexerMachine!.HandleEvent(new StateMachine.NewHead(toIndex));

                // As long as new items arrive, keep processing
            } while (Interlocked.CompareExchange(ref _newItemsArrived, 0, 1) == 1);
        }
        catch (OperationCanceledException)
        {
            _indexerContext!.Logger.Info("Processing was canceled.");
        }
        catch (Exception e)
        {
            _indexerContext!.Logger.Error("Error processing blocks", e);
            throw;
        }
        finally
        {
            // Mark processing as complete
            Interlocked.Exchange(ref _isProcessing, 0);
        }
    }

    public Task InitNetworkProtocol()
    {
        return Task.CompletedTask;
    }

    public async Task InitRpcModules()
    {
        await Task.Delay(5000);

        if (_indexerContext?.NethermindApi.RpcModuleProvider == null)
        {
            throw new Exception("_indexerContext.NethermindApi.RpcModuleProvider is not set");
        }

        var (getFromAPi, _) = _indexerContext.NethermindApi.ForRpc;

        CirclesRpcModule circlesRpcModule = new(_indexerContext);
        getFromAPi.RpcModuleProvider?.Register(
            new SingletonModulePool<ICirclesRpcModule>(circlesRpcModule));

        if (getFromAPi.SubscriptionFactory == null)
        {
            throw new Exception("getFromAPi.SubscriptionFactory is not set");
        }

        getFromAPi.SubscriptionFactory.RegisterSubscriptionType<CirclesSubscriptionParams>(
            "circles",
            (client, param) => new CirclesSubscription(client, _indexerContext, param));
    }

    public ValueTask DisposeAsync()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();

        return ValueTask.CompletedTask;
    }
}