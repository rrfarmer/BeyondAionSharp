using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Custom.Instance;
using Aion.GameServer.Model;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/CustomInstanceDAO (@author Jo, Estrayl). JDBC DAO over custom_instance (per-player ranked-arena rank + top10).
/// DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; SQL (incl. the joined top10 with
/// table-qualified column labels "c.player_id"/"p.name", INTERVAL 14 DAY, ORDER BY ... LIMIT 10) preserved VERBATIM. Timestamp&lt;-&gt;millis:
/// rset.getTimestamp(col).getTime() -> new DateTimeOffset(reader.GetDateTime(ord)).ToUnixTimeMilliseconds() (local-tz interpretation, as
/// JDBC); new Timestamp(millis) -> DateTimeOffset.FromUnixTimeMilliseconds(millis).LocalDateTime. PlayerClass.valueOf(name) ->
/// Enum.Parse&lt;PlayerClass&gt;(name); Race.toString() for the race param.
/// </summary>
public class CustomInstanceDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(CustomInstanceDAO));

    private const string SELECT_QUERY = "SELECT * FROM `custom_instance` WHERE ? = player_id";
    private const string UPDATE_QUERY = "REPLACE INTO `custom_instance` (`player_id`, `rank`, `last_entry`, `max_rank`, `dps`) VALUES (?,?,?,?,?)";
    private const string SELECT_TOP10_QUERY = "SELECT c.*, p.name, p.player_class FROM custom_instance c, players p WHERE c.player_id = p.id AND p.race = ? AND c.last_entry > NOW() - INTERVAL 14 DAY ORDER BY c.rank DESC, c.last_entry DESC LIMIT 10";

    public static CustomInstanceRank LoadPlayerRankObject(int playerId)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            using MySqlDataReader rset = stmt.ExecuteReader();
            if (rset.Read())
                return new CustomInstanceRank(playerId, rset.GetInt32(rset.GetOrdinal("rank")),
                    new DateTimeOffset(rset.GetDateTime(rset.GetOrdinal("last_entry"))).ToUnixTimeMilliseconds(),
                    rset.GetInt32(rset.GetOrdinal("max_rank")), rset.GetInt32(rset.GetOrdinal("dps")));
        }
        catch (Exception e)
        {
            log.LogError(e, "[CUSTOM_INSTANCE] Error loading rank object on player id " + playerId);
        }
        return null;
    }

    public static bool StorePlayer(CustomInstanceRank rankObj)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = UPDATE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = rankObj.GetPlayerId() });
            stmt.Parameters.Add(new MySqlParameter { Value = rankObj.GetRank() });
            stmt.Parameters.Add(new MySqlParameter { Value = DateTimeOffset.FromUnixTimeMilliseconds(rankObj.GetLastEntry()).LocalDateTime });
            stmt.Parameters.Add(new MySqlParameter { Value = rankObj.GetMaxRank() });
            stmt.Parameters.Add(new MySqlParameter { Value = rankObj.GetDps() });
            stmt.ExecuteNonQuery();
            return true;
        }
        catch (Exception e)
        {
            log.LogError(e, "[CUSTOM_INSTANCE] Error storing last entries on player id " + rankObj.GetPlayerId());
            return false;
        }
    }

    public static List<CustomInstanceRankedPlayer> LoadTop10(Race race)
    {
        List<CustomInstanceRankedPlayer> players = new List<CustomInstanceRankedPlayer>(10);
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_TOP10_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = race.ToString() });
            using MySqlDataReader rset = stmt.ExecuteReader();
            while (rset.Read())
            {
                int playerId = rset.GetInt32(rset.GetOrdinal("c.player_id"));
                int rank = rset.GetInt32(rset.GetOrdinal("c.rank"));
                long lastEntry = new DateTimeOffset(rset.GetDateTime(rset.GetOrdinal("c.last_entry"))).ToUnixTimeMilliseconds();
                int maxRank = rset.GetInt32(rset.GetOrdinal("c.max_rank"));
                int dps = rset.GetInt32(rset.GetOrdinal("c.dps"));
                string name = rset.GetString(rset.GetOrdinal("p.name"));
                PlayerClass playerClass = Enum.Parse<PlayerClass>(rset.GetString(rset.GetOrdinal("p.player_class")));
                players.Add(new CustomInstanceRankedPlayer(playerId, rank, lastEntry, maxRank, dps, name, playerClass));
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "[CUSTOM_INSTANCE] Error loading top 10 " + race + " players");
        }
        return players;
    }
}
