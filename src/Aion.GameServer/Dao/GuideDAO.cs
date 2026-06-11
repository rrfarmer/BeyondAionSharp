using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Guide;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/GuideDAO (@author xTz). JDBC DAO over the guides table (per-player guide/help entries).
/// DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; rset.getInt/getString("col") ->
/// reader.GetX(reader.GetOrdinal("col")); execute -> ExecuteNonQuery; SQL preserved verbatim (incl. saveGuide's space-less concat
/// "...player_id)"+"VALUES..."). getUsedIDs: Java uses a scrollable ResultSet (TYPE_SCROLL_INSENSITIVE; last()/getRow()/beforeFirst())
/// to pre-size an int[] - no ADO.NET analog, so the forward-only reader fills a List&lt;int&gt; then ToArray() (same result; scroll
/// flags dropped).
/// </summary>
public class GuideDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(GuideDAO));

    public const string DELETE_QUERY = "DELETE FROM `guides` WHERE `guide_id`=?";
    public const string SELECT_QUERY = "SELECT * FROM `guides` WHERE `player_id`=?";
    public const string SELECT_GUIDE_QUERY = "SELECT * FROM `guides` WHERE `guide_id`=? AND `player_id`=?";

    public static bool DeleteGuide(int guide_id)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = DELETE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = guide_id });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error delete guide_id: " + guide_id);
            return false;
        }
        return true;
    }

    public static List<Guide> LoadGuides(int playerId)
    {
        List<Guide> guides = new List<Guide>();
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            using MySqlDataReader rset = stmt.ExecuteReader();
            while (rset.Read())
            {
                int guide_id = rset.GetInt32(rset.GetOrdinal("guide_id"));
                int player_id = rset.GetInt32(rset.GetOrdinal("player_id"));
                string title = rset.GetString(rset.GetOrdinal("title"));
                Guide guide = new Guide(guide_id, player_id, title);
                guides.Add(guide);
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not restore Guide data for player: " + playerId + " from DB: " + e.Message);
        }
        return guides;
    }

    public static Guide LoadGuide(int player_id, int guide_id)
    {
        Guide guide = null;
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_GUIDE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = guide_id });
            stmt.Parameters.Add(new MySqlParameter { Value = player_id });
            using MySqlDataReader rset = stmt.ExecuteReader();
            while (rset.Read())
            {
                string title = rset.GetString(rset.GetOrdinal("title"));
                guide = new Guide(guide_id, player_id, title);
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not restore Survey data for player: " + player_id + " from DB: " + e.Message);
        }
        return guide;
    }

    public static void SaveGuide(int guide_id, Player player, string title)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "INSERT INTO guides(guide_id, title, player_id)" + "VALUES (?, ?, ?)";
            stmt.Parameters.Add(new MySqlParameter { Value = guide_id });
            stmt.Parameters.Add(new MySqlParameter { Value = title });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error saving playerName: " + player);
        }
    }

    public static int[] GetUsedIDs()
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "SELECT guide_id FROM guides";
            using MySqlDataReader rs = stmt.ExecuteReader();
            List<int> ids = new List<int>();
            while (rs.Read())
                ids.Add(rs.GetInt32(rs.GetOrdinal("guide_id")));
            return ids.ToArray();
        }
        catch (Exception e)
        {
            log.LogError(e, "Can't get list of IDs from guides table");
            return null;
        }
    }
}
