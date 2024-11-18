using System.Numerics;
using Circles.Index.Utils;

namespace Circles.Index.Graphs;

public record BalanceNode(
    string Address,
    string Token,
    BigInteger Amount,
    long LastChangeTimestamp) : Node(Address)
{
    public BigInteger DemurragedAmount =>
        Demurrage.ApplyDemurrage(Demurrage.InflationDayZero, LastChangeTimestamp, Amount);

    public string HolderAddress => Address.Split("-")[0];
}