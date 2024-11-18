using Circles.Index.Common;
using Circles.Index.EventSourcing;
using Circles.Pathfinder.Data;
using Circles.Pathfinder.DTOs;
using Moq;

namespace Circles.Pathfinder.Tests;

[TestFixture]
public class PathfinderTests
{
    public const string ConnectionString =
        "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";

    [Test]
    public async Task FindPath()
    {
        var loadGraph = new LoadGraph(ConnectionString);
        var graphFactory = new GraphFactory();
        var mockContext = new Mock<Context<Aggregates>>();
        var pathfinder = new V2Pathfinder(mockContext.Object, loadGraph, graphFactory);
        var flowRequest = new FlowRequest
        {
            Source = "0x32e69894af3a7d1124baa2d9f1fcd38d9d58fe4a",
            Sink = "0x43e96201714514f9a1dd5bd9fbe3f01c329af95a",
            TargetFlow = "9999999999999999999999999999999999999999"
        };
        var result = await pathfinder.ComputeMaxFlow(flowRequest);
        Assert.That(result.MaxFlow, Is.Not.EqualTo("0"));
    }
}