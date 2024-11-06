using Circles.Index.CirclesV2;
using Circles.Pathfinder.Data;
using Circles.Pathfinder.EventSourcing;
using Circles.Pathfinder.Edges;
using Circles.Pathfinder.Graphs;
using Nethermind.Int256;
using Npgsql;

namespace Circles.Pathfinder.Tests;

[TestFixture]
public class TrustGraphAggregatorTests
{
    private TrustGraphAggregator aggregator;

    private const string ConnectionString =
        "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres";

    [SetUp]
    public void SetUp()
    {
        aggregator = new TrustGraphAggregator();
    }

    [Test]
    public void LoadV2Data()
    {
        // Load a trust graph from individual events
        var trustEvents = new LoadGraph(ConnectionString).LoadV2TrustEvents();
        foreach (var trustEvent in trustEvents)
        {
            aggregator.ProcessEvent(trustEvent);
        }

        // Load the current trust graph from a database view
        var loadGraph = new LoadGraph(ConnectionString);
        var graphFactory = new GraphFactory();
        var trustGraph = graphFactory.V2TrustGraph(loadGraph);

        // Compare the two trust graphs
        Assert.That(aggregator.GetState().Edges.Count, Is.EqualTo(trustGraph.Edges.Count));
        Assert.That(aggregator.GetState().Nodes.Count, Is.EqualTo(trustGraph.Nodes.Count));
    }

    [Test]
    public void ProcessEvents_CanHandleEventsWithSameTimestamp()
    {
        var trustEvent1 = new Trust(1, 100, 0, 0, "0x", "0xTruster1", "0xTrustee1", new UInt256(200));
        var trustEvent2 = new Trust(2, 100, 0, 0, "0x", "0xTruster2", "0xTrustee2", new UInt256(200));

        aggregator.ProcessEvent(trustEvent1);
        aggregator.ProcessEvent(trustEvent2);

        Assert.That(aggregator.GetState().Edges.Count == 2);
        Assert.That(aggregator.GetState().Nodes.Count == 4);
    }

    [Test]
    public void InitialState_IsEmpty()
    {
        // Get the initial state
        var state = aggregator.GetState();

        // Assert that the graph is empty
        Assert.That(!state.Nodes.Any());
        Assert.That(!state.Edges.Any());
    }

    [Test]
    public void ProcessTrustEvent_AddsTrustEdge()
    {
        // Create a Trust event
        var trustEvent = new Trust(1, 100, 0, 0, "0x", "0xTruster", "0xTrustee", new UInt256(200));

        // Process the event
        aggregator.ProcessEvent(trustEvent);

        // Get the state
        var state = aggregator.GetState();

        // Assert that the trust edge is added
        Assert.That(state.Nodes.ContainsKey("0xTruster"));
        Assert.That(state.Nodes.ContainsKey("0xTrustee"));

        Assert.That(1, Is.EqualTo(state.Edges.Count));

        var trustEdge = new TrustEdge("0xTruster", "0xTrustee", new UInt256(200));
        Assert.That(state.Edges.Contains(trustEdge));
    }

    [Test]
    public void ProcessBlockEvent_RemovesExpiredTrustEdges()
    {
        // Create a Trust event
        var trustEvent = new Trust(1, 100, 0, 0, "0x", "0xTruster", "0xTrustee", new UInt256(150));

        // Process the Trust event
        aggregator.ProcessEvent(trustEvent);

        // Process a BlockEvent with timestamp after expiry
        var blockEvent = new BlockEvent(2, 160);

        aggregator.ProcessEvent(blockEvent);

        // Get the state
        var state = aggregator.GetState();

        // Assert that the trust edge is removed
        Assert.That(!state.Nodes.ContainsKey("0xTruster"));
        Assert.That(!state.Nodes.ContainsKey("0xTrustee"));

        Assert.That(!state.Edges.Any());
    }

    [Test]
    public void ProcessTrustEvent_WithPastExpiry_RemovesTrustEdge()
    {
        // Add the trust edge first to simulate existing trust
        var initialTrustEvent = new Trust(0, 50, 0, 0, "0x", "0xTruster", "0xTrustee", new UInt256(150));
        aggregator.ProcessEvent(initialTrustEvent);

        // Create a Trust event with expiry time in the past
        var trustEvent = new Trust(1, 200, 0, 0, "0x", "0xTruster", "0xTrustee", new UInt256(100));
        aggregator.ProcessEvent(trustEvent);

        // Get the state
        var state = aggregator.GetState();

        // Assert that the trust edge is removed
        Assert.That(!state.Edges.Any());
    }

    [Test]
    public void ProcessEvents_WithDecreasingTimestamp_ThrowsException()
    {
        // Create a Trust event with timestamp 100
        var trustEvent1 = new Trust(1, 100, 0, 0, "0x", "0xTruster1", "0xTrustee1", new UInt256(200));

        // Process the Trust event
        aggregator.ProcessEvent(trustEvent1);

        // Create another Trust event with earlier timestamp
        var trustEvent2 = new Trust(2, 90, 0, 0, "0x", "0xTruster2", "0xTrustee2", new UInt256(200));

        // Process the event and expect an exception
        Assert.Throws<InvalidOperationException>(() => aggregator.ProcessEvent(trustEvent2));
    }

    [Test]
    public void RevertToBlock_RevertsStateCorrectly()
    {
        // Create and process events
        var trustEvent1 = new Trust(1, 100, 0, 0, "0x", "0xTruster1", "0xTrustee1", new UInt256(200));
        aggregator.ProcessEvent(trustEvent1);

        var trustEvent2 = new Trust(2, 110, 0, 0, "0x", "0xTruster2", "0xTrustee2", new UInt256(200));
        aggregator.ProcessEvent(trustEvent2);

        // Revert to block 1
        aggregator.RevertToBlock(1, 200);

        // Get the state
        var state = aggregator.GetState();

        // Assert that only trustEvent1 is in the state
        Assert.That(state.Nodes.ContainsKey("0xTruster1"));
        Assert.That(state.Nodes.ContainsKey("0xTrustee1"));
        Assert.That(state.Edges.Contains(new TrustEdge("0xTruster1", "0xTrustee1", new UInt256(200))));

        Assert.That(!state.Nodes.ContainsKey("0xTruster2"));
        Assert.That(!state.Nodes.ContainsKey("0xTrustee2"));
        Assert.That(!state.Edges.Contains(new TrustEdge("0xTruster2", "0xTrustee2", new UInt256(200))));
    }

    [Test]
    public void ProcessMultipleTrustEvents_BuildsGraphCorrectly()
    {
        // Create and process multiple Trust events
        var trustEvent1 = new Trust(1, 100, 0, 0, "0x", "0xTruster1", "0xTrustee1", new UInt256(200));
        aggregator.ProcessEvent(trustEvent1);

        var trustEvent2 = new Trust(2, 110, 0, 0, "0x", "0xTrustee1", "0xTrustee2", new UInt256(200));
        aggregator.ProcessEvent(trustEvent2);

        // Get the state
        var state = aggregator.GetState();

        // Assert that all nodes and edges are present
        Assert.That(state.Nodes.ContainsKey("0xTruster1"));
        Assert.That(state.Nodes.ContainsKey("0xTrustee1"));
        Assert.That(state.Nodes.ContainsKey("0xTrustee2"));

        Assert.That(state.Edges.Contains(new TrustEdge("0xTruster1", "0xTrustee1", new UInt256(200))));
        Assert.That(state.Edges.Contains(new TrustEdge("0xTrustee1", "0xTrustee2", new UInt256(200))));
    }

    [Test]
    public void ProcessBlockEvent_RemovesMultipleExpiredTrustEdges()
    {
        // Create and process multiple Trust events with different expiry times
        var trustEvent1 = new Trust(1, 100, 0, 0, "0x", "0xTruster1", "0xTrustee1", new UInt256(150));
        aggregator.ProcessEvent(trustEvent1);

        var trustEvent2 = new Trust(2, 110, 0, 0, "0x", "0xTruster2", "0xTrustee2", new UInt256(160));
        aggregator.ProcessEvent(trustEvent2);

        var trustEvent3 = new Trust(3, 120, 0, 0, "0x", "0xTruster3", "0xTrustee3", new UInt256(170));
        aggregator.ProcessEvent(trustEvent3);

        // Process a BlockEvent with timestamp 155
        var blockEvent = new BlockEvent(3, 165);
        aggregator.ProcessEvent(blockEvent);

        // Get the state
        var state = aggregator.GetState();

        // Assert that the first trust edge is removed, second remains
        Assert.That(!state.Edges.Contains(new TrustEdge("0xTruster1", "0xTrustee1", new UInt256(150))));
        Assert.That(!state.Edges.Contains(new TrustEdge("0xTruster2", "0xTrustee2", new UInt256(160))));
        Assert.That(state.Edges.Contains(new TrustEdge("0xTruster3", "0xTrustee3", new UInt256(170))));
    }

    [Test]
    public void ProcessingExpiredTrust_DoesNotThrow()
    {
        // Create a Trust event with expiry time in the past
        var trustEvent = new Trust(1, 200, 0, 0, "0x", "0xTruster", "0xTrustee", new UInt256(100));

        // Process the Trust event and ensure no exception is thrown
        Assert.DoesNotThrow(() => aggregator.ProcessEvent(trustEvent));

        // Get the state
        var state = aggregator.GetState();

        // Assert that the trust edge is not present
        Assert.That(!state.Edges.Any());
    }

    [Test]
    public void RevertingToNonExistentBlock_ThrowsException()
    {
        // Attempt to revert to a block before any events have been processed
        Assert.Throws<ArgumentOutOfRangeException>(() => aggregator.RevertToBlock(0, 0));
    }

    [Test]
    public void RevertingToFutureBlock_ThrowsException()
    {
        // Process an event at block 1
        var trustEvent = new Trust(1, 100, 0, 0, "0x", "0xTruster", "0xTrustee", new UInt256(200));
        aggregator.ProcessEvent(trustEvent);

        // Attempt to revert to a block beyond the latest block
        Assert.Throws<ArgumentOutOfRangeException>(() => aggregator.RevertToBlock(2, 300));
    }

    [Test]
    public void StoreOnly12EventsInLog()
    {
        // Process 15 Trust events
        for (uint i = 0; i < 15; i++)
        {
            uint time = 100 * i;
            uint expiry = 100 * i + 50;

            var trustEvent = new Trust(i, time, 0, 0, "0x", "0xTruster" + i, "0xTrustee" + i, new UInt256(expiry));
            aggregator.ProcessEvent(trustEvent);
        }

        // Get the state
        var state = aggregator.GetState();

        // Make sure all edges have been written to the state
        Assert.That(state.Edges.Count, Is.EqualTo(15));

        // Make sure revert() fails when trying to revert to block 2 (block 2 would be the 13th block back)
        Assert.Throws<ArgumentOutOfRangeException>(() => aggregator.RevertToBlock(2, 200));

        // Make sure revert() succeeds when trying to revert to block 3
        aggregator.RevertToBlock(3, 300);

        // Make sure only 4 edges are left in the state (block 0, 1, 2, 3)
        state = aggregator.GetState();

        Assert.That(state.Edges.Count, Is.EqualTo(4));
    }
}