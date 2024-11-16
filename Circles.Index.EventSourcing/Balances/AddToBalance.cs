using System.Numerics;
using Circles.Index.Graphs;

namespace Circles.Index.EventSourcing.Balances;

public class AddToBalance(string accountAddress, string tokenAddress, BigInteger value, long timestamp)
    : IEventAction<BalanceGraph>
{
    public BalanceGraph Apply(BalanceGraph state)
    {
        var currentBalance = state.GetBalance(accountAddress, tokenAddress);
        state.SetBalance(accountAddress, tokenAddress, currentBalance + value, timestamp);

        return state;
    }

    public IEventAction<BalanceGraph> GetInverseAction()
    {
        return new SubtractFromBalance(accountAddress, tokenAddress, value, timestamp);
    }
}