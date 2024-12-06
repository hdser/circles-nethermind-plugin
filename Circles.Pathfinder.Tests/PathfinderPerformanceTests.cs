using System.Diagnostics;
using System.Numerics;
using Circles.Pathfinder.DTOs;

namespace Circles.Pathfinder.Tests;

[TestFixture]
public class PathfinderPerformanceTests
{
    private LoadGraph _loadGraph;
    private GraphFactory _graphFactory;
    private V2Pathfinder _pathfinder;
    private List<(string Source, string Sink)> _testScenarios;

    [SetUp]
    public void Setup()
    {
        _loadGraph = new LoadGraph(PathfinderTests.ConnectionString);
        _graphFactory = new GraphFactory();
        _pathfinder = new V2Pathfinder(_loadGraph, _graphFactory);
        
        // Define test scenarios
        _testScenarios = new List<(string Source, string Sink)>
        {
            ("0x42cedde51198d1773590311e2a340dc06b24cb37", "0xcadd4ea3bcc361fc4af2387937d7417be8d7dfc2"),
            // Add more scenarios as needed
        };
    }

    [Test]
    public async Task TestSinglePathPerformance()
    {
        var scenario = _testScenarios[0];
        var metrics = await MeasurePathfinding(scenario.Source, scenario.Sink);
        
        Console.WriteLine($"""
            Single Path Performance:
            Source: {metrics.Source}
            Sink: {metrics.Sink}
            Execution Time: {metrics.ExecutionTime.TotalMilliseconds:F2}ms
            Max Flow: {metrics.MaxFlow}
            Number of Transfers: {metrics.TransferCount}
            """);

        // Optional: Add assertions for performance thresholds
        Assert.That(metrics.ExecutionTime.TotalSeconds, Is.LessThan(5), "Pathfinding took too long");
        Assert.That(metrics.TransferCount, Is.GreaterThan(0), "No transfer path found");
    }

    [Test]
    public async Task TestMultipleSequentialPaths()
    {
        var results = new List<PathMetrics>();
        var totalStopwatch = Stopwatch.StartNew();

        foreach (var scenario in _testScenarios)
        {
            var metrics = await MeasurePathfinding(scenario.Source, scenario.Sink);
            results.Add(metrics);
        }

        totalStopwatch.Stop();

        // Calculate aggregate metrics
        var avgTime = TimeSpan.FromMilliseconds(results.Average(r => r.ExecutionTime.TotalMilliseconds));
        var avgTransfers = results.Average(r => r.TransferCount);
        var totalFlow = results.Sum(r => BigInteger.Parse(r.MaxFlow));

        Console.WriteLine($"""
            Multiple Paths Performance:
            Total Execution Time: {totalStopwatch.Elapsed.TotalSeconds:F2}s
            Average Request Time: {avgTime.TotalMilliseconds:F2}ms
            Average Transfers per Path: {avgTransfers:F2}
            Total Max Flow: {totalFlow}
            Number of Scenarios: {results.Count}
            """);
    }

    [Test]
    public async Task TestConcurrentPaths()
    {
        var concurrencyLevels = new[] { 2, 5, 10 };

        foreach (var concurrency in concurrencyLevels)
        {
            var totalStopwatch = Stopwatch.StartNew();
            var tasks = new List<Task<PathMetrics>>();
            
            // Start concurrent requests
            for (int i = 0; i < concurrency; i++)
            {
                var scenario = _testScenarios[i % _testScenarios.Count];
                tasks.Add(MeasurePathfinding(scenario.Source, scenario.Sink));
            }

            var results = await Task.WhenAll(tasks);
            totalStopwatch.Stop();

            var avgTime = TimeSpan.FromMilliseconds(results.Average(r => r.ExecutionTime.TotalMilliseconds));
            var avgTransfers = results.Average(r => r.TransferCount);
            var totalFlow = results.Sum(r => BigInteger.Parse(r.MaxFlow));

            Console.WriteLine($"""
                Concurrent Paths Performance (Concurrency: {concurrency}):
                Total Execution Time: {totalStopwatch.Elapsed.TotalSeconds:F2}s
                Average Request Time: {avgTime.TotalMilliseconds:F2}ms
                Average Transfers per Path: {avgTransfers:F2}
                Total Max Flow: {totalFlow}
                """);
        }
    }

    private async Task<PathMetrics> MeasurePathfinding(string source, string sink)
    {
        var stopwatch = Stopwatch.StartNew();
        
        var request = new FlowRequest
        {
            Source = source,
            Sink = sink,
            TargetFlow = "99999999999999999999999999999999999"
        };

        var result = await _pathfinder.ComputeMaxFlow(request);
        stopwatch.Stop();

        var metrics = new PathMetrics
        {
            Source = source,
            Sink = sink,
            ExecutionTime = stopwatch.Elapsed,
            MaxFlow = result.MaxFlow,
            TransferCount = result.Transfers.Count,
            Transfers = result.Transfers
        };

        ValidateFlowConservation(metrics);
        return metrics;
    }

    private void ValidateFlowConservation(PathMetrics metrics)
    {
        var flowBalance = new Dictionary<string, BigInteger>();

        // Calculate net flow for each address
        foreach (var transfer in metrics.Transfers)
        {
            var amount = BigInteger.Parse(transfer.Value);

            if (!flowBalance.ContainsKey(transfer.From))
                flowBalance[transfer.From] = BigInteger.Zero;
            flowBalance[transfer.From] -= amount;

            if (!flowBalance.ContainsKey(transfer.To))
                flowBalance[transfer.To] = BigInteger.Zero;
            flowBalance[transfer.To] += amount;
        }

        // Validate flow conservation
        foreach (var (address, balance) in flowBalance)
        {
            if (address == metrics.Source)
            {
                Assert.That(balance, Is.LessThan(BigInteger.Zero), 
                    $"Source {address} should have negative net flow");
            }
            else if (address == metrics.Sink)
            {
                Assert.That(balance, Is.GreaterThan(BigInteger.Zero), 
                    $"Sink {address} should have positive net flow");
            }
            else
            {
                Assert.That(balance, Is.EqualTo(BigInteger.Zero), 
                    $"Intermediate node {address} should have zero net flow, but has {balance}");
            }
        }
    }

    private class PathMetrics
    {
        public string Source { get; set; }
        public string Sink { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public string MaxFlow { get; set; }
        public int TransferCount { get; set; }
        public List<TransferPathStep> Transfers { get; set; }
    }
}