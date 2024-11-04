using Npgsql;
using System.Diagnostics;

namespace Circles.Pathfinder.Data
{
    // TODO: Use CirclesQuery<T> and remove the Npgsql dependency
    public class LoadGraph(string connectionString)
    {
        public IEnumerable<(string Balance, string Account, string TokenAddress)> LoadV2Balances()
        {
            var balanceQuery = @"
                select ""totalBalance""::text, ""account"", ""tokenOwner""
                from ""V_CrcV1_BalancesByAccountAndToken""
                where ""totalBalance"" > 0;
            ";

            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            using var command = new NpgsqlCommand(balanceQuery, connection);

            var stopwatch = Stopwatch.StartNew();

            using var reader = command.ExecuteReader();

            stopwatch.Stop();
            var executeReaderTime = stopwatch.Elapsed;
            Console.WriteLine($"Time taken to execute LoadV2Balances query: {executeReaderTime.TotalMilliseconds} ms");

            while (reader.Read())
            {
                var balance = reader.GetString(0);
                var account = reader.GetString(1);
                var tokenAddress = reader.GetString(2);

                yield return (balance, account, tokenAddress);
            }
        }

        public IEnumerable<(string Truster, string Trustee, int Limit)> LoadV2Trust()
        {
            var trustQuery = @"
                select ""canSendTo"" as ""truster"", ""user"" as ""trustee""
                from ""V_CrcV1_TrustRelations"";
            ";

            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            using var command = new NpgsqlCommand(trustQuery, connection);

            var stopwatch = Stopwatch.StartNew();

            using var reader = command.ExecuteReader();

            stopwatch.Stop();
            var executeReaderTime = stopwatch.Elapsed;
            Console.WriteLine($"Time taken to execute LoadV2Trust query: {executeReaderTime.TotalMilliseconds} ms");

            while (reader.Read())
            {
                var truster = reader.GetString(0);
                var trustee = reader.GetString(1);

                yield return (truster, trustee, 100); // Assuming a default trust limit of 100 in V2
            }
        }
    }
}
