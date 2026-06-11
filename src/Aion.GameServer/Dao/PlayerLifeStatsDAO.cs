using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Stats.Container;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/PlayerLifeStatsDAO (@author Mr. Poke). JDBC DAO over player_life_stats (persisted current hp/mp/fp).
/// DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; rset.getInt("col") ->
/// reader.GetInt32(reader.GetOrdinal("col")); execute -> ExecuteNonQuery; SQL verbatim. loadPlayerLifeStat falls through to
/// insertPlayerLifeStat when no row exists, exactly as Java.
/// </summary>
public class PlayerLifeStatsDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(PlayerLifeStatsDAO));
    public const string INSERT_QUERY = "INSERT INTO `player_life_stats` (`player_id`, `hp`, `mp`, `fp`) VALUES (?,?,?,?)";
    public const string SELECT_QUERY = "SELECT `hp`, `mp`, `fp` FROM `player_life_stats` WHERE `player_id`=?";
    public const string UPDATE_QUERY = "UPDATE player_life_stats set `hp`=?, `mp`=?, `fp`=? WHERE `player_id`=?";

    public static void LoadPlayerLifeStat(Player player)
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
            {
                PlayerLifeStats lifeStats = player.GetLifeStats();
                lifeStats.SetCurrentHp(rset.GetInt32(rset.GetOrdinal("hp")));
                lifeStats.SetCurrentMp(rset.GetInt32(rset.GetOrdinal("mp")));
                lifeStats.SetCurrentFp(rset.GetInt32(rset.GetOrdinal("fp")));
            }
            else
                InsertPlayerLifeStat(player);
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not restore PlayerLifeStat data for playerObjId: " + player.GetObjectId() + " from DB: " + e.Message);
        }
    }

    public static void InsertPlayerLifeStat(Player player)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = INSERT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetLifeStats().GetCurrentHp() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetLifeStats().GetCurrentMp() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetLifeStats().GetCurrentFp() });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not store PlayerLifeStat data for player " + player.GetObjectId() + " from DB: " + e.Message);
        }
    }

    public static void UpdatePlayerLifeStat(Player player)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = UPDATE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetLifeStats().GetCurrentHp() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetLifeStats().GetCurrentMp() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetLifeStats().GetCurrentFp() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not update PlayerLifeStat data for player " + player.GetObjectId() + " from DB: " + e.Message);
        }
    }
}
