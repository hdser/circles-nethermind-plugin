using System.Numerics;
using Circles.Index.Utils;

namespace Circles.Index.Graphs;

public class BalanceNode(string address, string token, BigInteger amount)
    : Node(address + "-" + token)
{
    public string Token { get; } = token;
    public BigInteger Amount { get; set; } = amount;

    /// <summary>
    /// When the balance of the holder was last changed.
    /// </summary>
    public long LastChangeTimestamp { get; set; }

    /// <summary>
    /// Gets the demurraged amount.
    /// </summary>
    public BigInteger DemurragedAmount =>
        Demurrage.ApplyDemurrage(Demurrage.InflationDayZero, LastChangeTimestamp, Amount);

    public string HolderAddress => Address.Split("-")[0];
}