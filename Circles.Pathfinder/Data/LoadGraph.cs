using Circles.Index.CirclesV2;
using Nethermind.Int256;
using Npgsql;

namespace Circles.Pathfinder.Data
{
    // TODO: Use CirclesQuery<T> and remove the Npgsql dependency
    public class LoadGraph(string connectionString)
    {
        public IEnumerable<(string Balance, string Account, string TokenAddress)> LoadV2Balances()
        {
            var balanceQuery = @"
                select ""demurragedTotalBalance""::text, ""account"", ""tokenAddress""
                from ""V_CrcV2_BalancesByAccountAndToken""
                where ""demurragedTotalBalance"" > 0;
            ";

            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            using var command = new NpgsqlCommand(balanceQuery, connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var balance = reader.GetString(0);
                var account = reader.GetString(1);
                var tokenAddress = reader.GetString(2);

                yield return (balance, account, tokenAddress);
            }
        }

        public IEnumerable<(string Truster, string Trustee, UInt256 ExpiryTime)> LoadV2Trust()
        {
            var trustQuery = @"
                select truster, trustee, ""expiryTime""::text
                from ""V_CrcV2_TrustRelations"";
            ";

            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            using var command = new NpgsqlCommand(trustQuery, connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var truster = reader.GetString(0);
                var trustee = reader.GetString(1);
                var expiryTime = UInt256.Parse(reader.GetString(2));

                yield return (truster, trustee, expiryTime);
            }
        }

        public IEnumerable<Trust> LoadV2TrustEvents(long? fromBlock = null, long? toBlock = null)
        {
            var trustEventsQuery = $@"
                select ""blockNumber"",
                       timestamp,
                       ""transactionIndex"",
                       ""logIndex"",
                       ""transactionHash"",
                       trustee,
                       truster,
                       ""expiryTime""::text
                from ""V_CrcV2_TrustRelations""
                where {(fromBlock is null ? "1 = 1" : $"\"blockNumber\" >= {fromBlock}")}
                  and {(toBlock is null ? "2 = 2" : $"\"blockNumber\" <= {toBlock}")}
                order by ""blockNumber"", ""transactionIndex"", ""logIndex"";
            ";

            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            using var command = new NpgsqlCommand(trustEventsQuery, connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var blockNumber = reader.GetInt32(0);
                var timestamp = reader.GetInt64(1);
                var transactionIndex = reader.GetInt32(2);
                var logIndex = reader.GetInt32(3);
                var transactionHash = reader.GetString(4);
                var trustee = reader.GetString(5);
                var truster = reader.GetString(6);
                var expiryTime = UInt256.Parse(reader.GetString(7));

                yield return new Trust(
                    blockNumber,
                    timestamp,
                    transactionIndex,
                    logIndex,
                    transactionHash,
                    truster,
                    trustee,
                    expiryTime);
            }
        }
    }
}