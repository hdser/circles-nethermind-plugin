using System.Collections.Concurrent;
using Nethermind.Int256;

namespace Circles.Index.Data.Cache;

public class BalanceCache
{
    public readonly ConcurrentDictionary<string, ConcurrentDictionary<string, UInt256>>
        _balancesPerAccountAndToken = new(Environment.ProcessorCount, Settings.InitialUserCacheSize);

    public void In(string account, string token, UInt256 amount)
    {
        if (!_balancesPerAccountAndToken.TryGetValue(account, out ConcurrentDictionary<string, UInt256>? tokenBalances))
        {
            tokenBalances = new ConcurrentDictionary<string, UInt256>();
            _balancesPerAccountAndToken[account] = tokenBalances;
        }

        tokenBalances[token] = tokenBalances.TryGetValue(token, out UInt256 balance)
            ? balance + amount
            : amount;
    }

    public void Out(string account, string token, UInt256 amount)
    {
        if (!_balancesPerAccountAndToken.TryGetValue(account, out ConcurrentDictionary<string, UInt256>? tokenBalances))
        {
            tokenBalances = new ConcurrentDictionary<string, UInt256>();
            _balancesPerAccountAndToken[account] = tokenBalances;
        }

        tokenBalances[token] = tokenBalances.TryGetValue(token, out UInt256 balance)
            ? balance - amount
            : amount;
    }

    public void Remove(string affectedUser, string affectedToken)
    {
        if (!_balancesPerAccountAndToken.TryGetValue(affectedUser,
                out ConcurrentDictionary<string, UInt256>? tokenBalances))
        {
            return;
        }

        tokenBalances.TryRemove(affectedToken, out _);
    }
}