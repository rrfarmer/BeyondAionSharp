using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects.Player;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/VeteranRewardDAO (@author Neon). JDBC DAO over player_veteran_rewards (received months per player).
/// DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; if(rset.next()) -> if(reader.Read());
/// rset.getInt("col") -> reader.GetInt32(reader.GetOrdinal("col")); execute -> ExecuteNonQuery; SQL verbatim. Load error-sentinel -1.
/// </summary>
public class VeteranRewardDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(VeteranRewardDAO));

    private const string SELECT_QUERY = "SELECT `received_months` FROM `player_veteran_rewards` WHERE `player_id`=?";
    private const string UPDATE_QUERY = "REPLACE INTO `player_veteran_rewards` (`player_id`, `received_months`) VALUES (?,?)";

    public static int LoadReceivedMonths(Player player)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
            using MySqlDataReader rset = stmt.ExecuteReader();
            if (rset.Read())
                return rset.GetInt32(rset.GetOrdinal("received_months"));
            return 0;
        }
        catch (Exception e)
        {
            log.LogError(e, "Error loading received veteran reward months for player " + player);
            return -1;
        }
    }

    public static bool StoreReceivedMonths(Player player, int months)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = UPDATE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
            stmt.Parameters.Add(new MySqlParameter { Value = months });
            stmt.ExecuteNonQuery();
            return true;
        }
        catch (Exception e)
        {
            log.LogError(e, "Error saving received veteran reward months (" + months + ") for player " + player);
            return false;
        }
    }
}
