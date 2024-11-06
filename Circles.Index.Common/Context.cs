using Nethermind.Api;
using Nethermind.Logging;

namespace Circles.Index.Common;

public record Context<TAggregates>(
    INethermindApi NethermindApi,
    InterfaceLogger Logger,
    Settings Settings,
    IDatabase Database,
    ILogParser[] LogParsers,
    Sink Sink,
    TAggregates Aggregates);