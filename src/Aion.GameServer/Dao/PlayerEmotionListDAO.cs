using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.Players.Emotion;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/PlayerEmotionListDAO (@author Mr. Poke). JDBC DAO over player_emotions (per-player learned emotions).
/// DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; rset.getInt("col") ->
/// reader.GetInt32(reader.GetOrdinal("col")); execute -> ExecuteNonQuery; SQL verbatim. emotions.add(id, remaining, false) ->
/// EmotionList.Add(int, int, bool). Java's "delete title" log message in deleteEmotion is a copy-paste quirk preserved literally.
/// </summary>
public class PlayerEmotionListDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(PlayerEmotionListDAO));

    public const string INSERT_QUERY = "INSERT INTO `player_emotions` (`player_id`, `emotion`, `remaining`) VALUES (?,?,?)";
    public const string SELECT_QUERY = "SELECT `emotion`, `remaining` FROM `player_emotions` WHERE `player_id`=?";
    public const string DELETE_QUERY = "DELETE FROM `player_emotions` WHERE `player_id`=? AND `emotion`=?";

    public static void LoadEmotions(Player player)
    {
        EmotionList emotions = new EmotionList(player);
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
                int emotionId = rset.GetInt32(rset.GetOrdinal("emotion"));
                int remaining = rset.GetInt32(rset.GetOrdinal("remaining"));
                emotions.Add(emotionId, remaining, false);
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not restore emotionId for playerObjId: " + player.GetObjectId() + " from DB: " + e.Message);
        }
        player.SetEmotions(emotions);
    }

    public static void InsertEmotion(Player player, Emotion emotion)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = INSERT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
            stmt.Parameters.Add(new MySqlParameter { Value = emotion.GetId() });
            stmt.Parameters.Add(new MySqlParameter { Value = emotion.GetExpireTime() });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not store emotionId for player " + player.GetObjectId() + " from DB: " + e.Message);
        }
    }

    public static void DeleteEmotion(int playerId, int emotionId)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = DELETE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            stmt.Parameters.Add(new MySqlParameter { Value = emotionId });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not delete title for player " + playerId + " from DB: " + e.Message);
        }
    }
}
