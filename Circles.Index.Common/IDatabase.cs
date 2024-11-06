using System.Data;
using System.Text.RegularExpressions;
using Nethermind.Core.Crypto;

namespace Circles.Index.Common;

public interface IDatabaseUtils
{
    public IDatabaseSchema Schema { get; }

    public string QuoteIdentifier(string identifier)
    {
        if (!Regex.IsMatch(identifier, @"^[a-zA-Z0-9_]+$"))
        {
            throw new ArgumentException("Invalid identifier");
        }

        return $"\"{identifier}\"";
    }

    IDbDataParameter CreateParameter(string? name, object? value);
}

public interface IDatabase : IDatabaseUtils
{
    void Migrate();

    /// <summary>
    /// Deletes all blocks beginning from (and including) the specified block number.
    /// </summary>
    /// <param name="reorgAt">The block number to start deleting from.</param>
    Task DeleteFromBlockOnwards(long reorgAt);

    Task WriteBatch(string @namespace, string table, IEnumerable<object> data, ISchemaPropertyMap propertyMap);
    long? LatestBlock();
    long? FirstGap();
    /// <summary>
    /// Gets the timestamp of the block with the specified number.
    /// </summary>
    /// <param name="blockNumber">The block number.</param>
    /// <returns>The timestamp of the block.</returns>
    long GetBlockTimestampByNumber(long blockNumber);
    IEnumerable<(long BlockNumber, Hash256 BlockHash)> LastPersistedBlocks(int count);
    DatabaseQueryResult Select(ParameterizedSql select);
}