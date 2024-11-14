using Circles.Index.Common;

namespace Circles.Index.EventSourcing;

public class BlockEvent(long blockNumber, long timestamp) : IIndexEvent
{
    public long BlockNumber => blockNumber;
    public long Timestamp => timestamp;
    public int TransactionIndex => -1;
    public int LogIndex => -1;
}