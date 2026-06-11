using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.GameObjects.Player.Title;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/PlayerTitleListDAO (@author xavier). JDBC DAO over player_titles. MIXED: loadTitleList via commons DB callback
/// helper (DB.Select(ParamReadStH), anonymous->nested LoadHandler); storeTitles/removeTitle via DatabaseFactory. setInt->Parameters.Add;
/// rset.next()/getInt->Read()/GetInt32(GetOrdinal); TitleList.AddEntry(id, remaining); Title.GetId/GetExpireTime. SQL verbatim. The
/// "Could not store emotionId" copy-paste log message preserved literally.
/// </summary>
public class PlayerTitleListDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(PlayerTitleListDAO));

    private const string LOAD_QUERY = "SELECT `title_id`, `remaining` FROM `player_titles` WHERE `player_id`=?";
    private const string INSERT_QUERY = "INSERT INTO `player_titles`(`player_id`,`title_id`, `remaining`) VALUES (?,?,?)";
    private const string DELETE_QUERY = "DELETE FROM `player_titles` WHERE `player_id`=? AND `title_id` =?;";

    public static TitleList LoadTitleList(int playerId)
    {
        TitleList tl = new TitleList();

        DB.Select(LOAD_QUERY, new LoadHandler(playerId, tl));
        return tl;
    }

    private sealed class LoadHandler : ParamReadStH
    {
        private readonly int playerId;
        private readonly TitleList tl;

        internal LoadHandler(int playerId, TitleList tl)
        {
            this.playerId = playerId;
            this.tl = tl;
        }

        public void SetParams(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
        }

        public void HandleRead(MySqlDataReader rset)
        {
            while (rset.Read())
            {
                int id = rset.GetInt32(rset.GetOrdinal("title_id"));
                int remaining = rset.GetInt32(rset.GetOrdinal("remaining"));
                tl.AddEntry(id, remaining);
            }
        }
    }

    public static bool StoreTitles(Player player, Title entry)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = INSERT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
            stmt.Parameters.Add(new MySqlParameter { Value = entry.GetId() });
            stmt.Parameters.Add(new MySqlParameter { Value = entry.GetExpireTime() });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not store emotionId for player " + player.GetObjectId() + " from DB: " + e.Message);
            return false;
        }
        return true;
    }

    public static bool RemoveTitle(int playerId, int titleId)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = DELETE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            stmt.Parameters.Add(new MySqlParameter { Value = titleId });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not delete title for player " + playerId + " from DB: " + e.Message);
            return false;
        }
        return true;
    }
}
