using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/PortalCooldownsDAO. JDBC DAO over portal_cooldowns (per-player instance-portal reuse timers + entry counts).
/// DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; rset.getInt/getLong("col") ->
/// reader.GetInt32/GetInt64(reader.GetOrdinal("col")); execute -> ExecuteNonQuery; SQL verbatim. Map&lt;Integer,PortalCooldown&gt; ->
/// Dictionary&lt;int,PortalCooldown&gt;; Map.put -> indexer; entrySet() -> foreach KeyValuePair. System.currentTimeMillis() ->
/// DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(). PortalCooldownList.SetPortalCoolDowns/GetPortalCoolDowns are tolerated red refs
/// (the reworked C# list exposes Add/Remove/Get instead). Load keeps only still-future entries; store = delete-then-reinsert (verbatim).
/// </summary>
public class PortalCooldownsDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(PortalCooldownsDAO));

    public const string INSERT_QUERY = "INSERT INTO `portal_cooldowns` (`player_id`, `world_id`, `reuse_time`, `entry_count`) VALUES (?,?,?,?)";
    public const string DELETE_QUERY = "DELETE FROM `portal_cooldowns` WHERE `player_id`=?";
    public const string SELECT_QUERY = "SELECT `world_id`, `reuse_time`, `entry_count` FROM `portal_cooldowns` WHERE `player_id`=?";

    public static void LoadPortalCooldowns(Player player)
    {
        Dictionary<int, PortalCooldown> portalCoolDowns = new Dictionary<int, PortalCooldown>();
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
                int worldId = rset.GetInt32(rset.GetOrdinal("world_id"));
                long reuseTime = rset.GetInt64(rset.GetOrdinal("reuse_time"));
                int entryCount = rset.GetInt32(rset.GetOrdinal("entry_count"));
                if (reuseTime > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                {
                    portalCoolDowns[worldId] = new PortalCooldown(worldId, reuseTime, entryCount);
                }
            }
            player.GetPortalCooldownList().SetPortalCoolDowns(portalCoolDowns);
        }
        catch (Exception e)
        {
            log.LogError(e, "LoadPortalCooldowns");
        }
    }

    public static void StorePortalCooldowns(Player player)
    {
        DeletePortalCooldowns(player);
        Dictionary<int, PortalCooldown> portalCoolDowns = player.GetPortalCooldownList().GetPortalCoolDowns();

        if (portalCoolDowns == null)
            return;

        foreach (KeyValuePair<int, PortalCooldown> entry in portalCoolDowns)
        {
            int worldId = entry.Key;
            long reuseTime = entry.Value.GetReuseTime();
            int entryCount = entry.Value.GetEnterCount();

            if (reuseTime < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                continue;

            try
            {
                using MySqlConnection con = DatabaseFactory.GetConnection();
                con.Open();
                using MySqlCommand stmt = con.CreateCommand();
                stmt.CommandText = INSERT_QUERY;
                stmt.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
                stmt.Parameters.Add(new MySqlParameter { Value = worldId });
                stmt.Parameters.Add(new MySqlParameter { Value = reuseTime });
                stmt.Parameters.Add(new MySqlParameter { Value = entryCount });
                stmt.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                log.LogError(e, "storePortalCooldowns");
            }
        }
    }

    private static void DeletePortalCooldowns(Player player)
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
            log.LogError(e, "deletePortalCooldowns");
        }
    }
}
