using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/InGameShopLogDAO (@author ViAl). Single-insert audit logger for the ingameshop_log table.
/// java.sql.Timestamp -> DateTime; setLong -> long MySqlParameter; executeUpdate -> ExecuteNonQuery; positional '?' -> ordered
/// MySqlParameter; INSERT SQL preserved verbatim. catch (SQLException) -> catch (Exception) logging the error. Java method name
/// log() -> Log() (distinct from the lowercase log field; C# is case-sensitive).
/// </summary>
public class InGameShopLogDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(InGameShopLogDAO));

    private const string INSERT_QUERY = "INSERT INTO `ingameshop_log` (`transaction_type`, `transaction_date`, `payer_name`, `payer_account_name`, `receiver_name`, `item_id`, `item_count`, `item_price`) VALUES (?, ?, ?, ?, ?, ?, ?, ?)";

    public static void Log(string transactionType, DateTime transactionDate, string payerName, string payerAccountName, string receiverName, int itemId,
        long itemCount, long itemPrice)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = INSERT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = transactionType });
            stmt.Parameters.Add(new MySqlParameter { Value = transactionDate });
            stmt.Parameters.Add(new MySqlParameter { Value = payerName });
            stmt.Parameters.Add(new MySqlParameter { Value = payerAccountName });
            stmt.Parameters.Add(new MySqlParameter { Value = receiverName });
            stmt.Parameters.Add(new MySqlParameter { Value = itemId });
            stmt.Parameters.Add(new MySqlParameter { Value = itemCount });
            stmt.Parameters.Add(new MySqlParameter { Value = itemPrice });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not insert ingameshop log");
        }
    }
}
