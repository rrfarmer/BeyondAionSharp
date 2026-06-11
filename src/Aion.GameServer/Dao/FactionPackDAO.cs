using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/FactionPackDAO (@author Estrayl). JDBC DAO over faction_packs (per-account faction-pack receiving player).
/// DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; rset.getInt("col") ->
/// reader.GetInt32(reader.GetOrdinal("col")); execute -> ExecuteNonQuery; SQL verbatim. Integer.MAX_VALUE error-sentinel -> int.MaxValue.
/// </summary>
public class FactionPackDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(FactionPackDAO));

    private const string UPDATE_QUERY = "REPLACE INTO `faction_packs` (`account_id`, `receiving_player`) VALUES (?,?)";
    private const string SELECT_QUERY = "SELECT `receiving_player` FROM `faction_packs` WHERE `account_id`=?";

    public static int LoadReceivingPlayer(int accountId)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = accountId });
            using MySqlDataReader rset = stmt.ExecuteReader();
            while (rset.Read())
                return rset.GetInt32(rset.GetOrdinal("receiving_player"));
            return 0;
        }
        catch (Exception e)
        {
            log.LogError(e, "[FACTION_PACK] Error loading received player id on account id " + accountId);
            return int.MaxValue;
        }
    }

    public static bool StoreReceivingPlayer(int accountId, int playerId)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = UPDATE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = accountId });
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            stmt.ExecuteNonQuery();
            return true;
        }
        catch (Exception e)
        {
            log.LogError(e, "[FACTION_PACK] Error saving received player id " + playerId + " on account id " + accountId);
            return false;
        }
    }
}
