using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/HouseObjectCooldownsDAO (@author Rolandas). JDBC DAO over house_object_cooldowns (per-player house-object reuse
/// timers). DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; rset.getInt/getLong("col")
/// -> reader.GetInt32/GetInt64(reader.GetOrdinal("col")); execute -> ExecuteNonQuery; SQL verbatim. player.getHouseObjectCooldowns()
/// Cooldowns: Map.put -> Cooldowns.Put, Map.entrySet() -> foreach over IEnumerable&lt;KeyValuePair&lt;int,long&gt;&gt;.
/// System.currentTimeMillis() -> DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(). Load filters to still-future entries (verbatim).
/// </summary>
public class HouseObjectCooldownsDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(HouseObjectCooldownsDAO));

    public const string INSERT_QUERY = "INSERT INTO `house_object_cooldowns` (`player_id`, `object_id`, `reuse_time`) VALUES (?,?,?)";
    public const string DELETE_QUERY = "DELETE FROM `house_object_cooldowns` WHERE `player_id`=?";
    public const string SELECT_QUERY = "SELECT `object_id`, `reuse_time` FROM `house_object_cooldowns` WHERE `player_id`=?";

    public static void LoadHouseObjectCooldowns(Player player)
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
                long reuseTime = rset.GetInt64(rset.GetOrdinal("reuse_time"));
                if (reuseTime > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                    player.GetHouseObjectCooldowns().Put(rset.GetInt32(rset.GetOrdinal("object_id")), reuseTime);
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "LoadHouseObjectCooldowns");
        }
    }

    public static void StoreHouseObjectCooldowns(Player player)
    {
        DeleteHouseObjectCoolDowns(player);

        foreach (KeyValuePair<int, long> entry in player.GetHouseObjectCooldowns())
        {
            int templateId = entry.Key;
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
                stmt.Parameters.Add(new MySqlParameter { Value = templateId });
                stmt.Parameters.Add(new MySqlParameter { Value = reuseTime });
                stmt.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                log.LogError(e, "storeHouseObjectCoolDowns");
            }
        }
    }

    private static void DeleteHouseObjectCoolDowns(Player player)
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
            log.LogError(e, "deleteHouseObjectCoolDowns");
        }
    }
}
