using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects.Player;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/CraftCooldownsDAO (@author synchro2). JDBC DAO over craft_cooldowns (per-player recipe reuse timers).
/// DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; rset.getInt/getLong("col") ->
/// reader.GetInt32/GetInt64(reader.GetOrdinal("col")); execute -> ExecuteNonQuery; SQL verbatim. player.getCraftCooldowns() Cooldowns:
/// Map.put -> Cooldowns.Put; Map.entrySet() -> foreach over IEnumerable&lt;KeyValuePair&lt;int,long&gt;&gt;. System.currentTimeMillis()
/// -> DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(). storeCraftCooldowns deletes then re-inserts only still-future entries (verbatim).
/// </summary>
public class CraftCooldownsDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(CraftCooldownsDAO));

    public const string INSERT_QUERY = "INSERT INTO `craft_cooldowns` (`player_id`, `delay_id`, `reuse_time`) VALUES (?,?,?)";
    public const string DELETE_QUERY = "DELETE FROM `craft_cooldowns` WHERE `player_id`=?";
    public const string SELECT_QUERY = "SELECT `delay_id`, `reuse_time` FROM `craft_cooldowns` WHERE `player_id`=?";

    public static void LoadCraftCooldowns(Player player)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
            using MySqlDataReader rset = stmt.ExecuteReader();
            while (rset.Read())
            {
                int delayId = rset.GetInt32(rset.GetOrdinal("delay_id"));
                long reuseTime = rset.GetInt64(rset.GetOrdinal("reuse_time"));
                player.GetCraftCooldowns().Put(delayId, reuseTime);
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Couldn't load craft cooldowns for " + player);
        }
    }

    public static void StoreCraftCooldowns(Player player)
    {
        DeleteCraftCoolDowns(player);

        foreach (KeyValuePair<int, long> entry in player.GetCraftCooldowns())
        {
            int delayId = entry.Key;
            long reuseTime = entry.Value;

            if (reuseTime < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                continue;

            try
            {
                using MySqlConnection con = DatabaseFactory.GetConnection();
                con.Open();
                using MySqlCommand stmt = con.CreateCommand();
                stmt.CommandText = INSERT_QUERY;
                stmt.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
                stmt.Parameters.Add(new MySqlParameter { Value = delayId });
                stmt.Parameters.Add(new MySqlParameter { Value = reuseTime });
                stmt.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                log.LogError(e, "Couldn't store craft cooldowns for " + player);
            }
        }
    }

    private static void DeleteCraftCoolDowns(Player player)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = DELETE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Couldn't delete craft cooldowns for " + player);
        }
    }
}
