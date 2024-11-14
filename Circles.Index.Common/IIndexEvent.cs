using Nethermind.Int256;

namespace Circles.Index.Common;

public interface IIndexEvent
{
    long BlockNumber { get; }
    long Timestamp { get; }
    int TransactionIndex { get; }
    int LogIndex { get; }
}

public record TransferEvent(
    long BlockNumber,
    long Timestamp,
    int TransactionIndex,
    int LogIndex,
    int BatchIndex,
    string From,
    string To,
    string TokenAddress,
    UInt256 Value) : IIndexEvent;