using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;

namespace Aion.GameServer.Dao;

public class BookmarkDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(BookmarkDAO));

    private const string LOAD_QUERY = "SELECT * FROM `bookmark` where player_id= ?";
    private const string STORE_QUERY = "REPLACE INTO `bookmark` (player_id, name, world_id, x, y, z) VALUES (?, ?, ?, ?, ?, ?)";
    private const string DELETE_QUERY = "DELETE FROM `bookmark` WHERE player_id = ? and name = ?";
    private const string DELETE_ALL_QUERY = "DELETE FROM `bookmark` WHERE player_id = ?";

    public static List<Bookmark> LoadBookmarks(int playerId)
    {
        var bookmarks = new List<Bookmark>();
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = LOAD_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            using MySqlDataReader rs = stmt.ExecuteReader();
            while (rs.Read())
            {
                bookmarks.Add(new Bookmark(
                    rs.GetString(rs.GetOrdinal("name")),
                    rs.GetInt32(rs.GetOrdinal("world_id")),
                    rs.GetFloat(rs.GetOrdinal("x")),
                    rs.GetFloat(rs.GetOrdinal("y")),
                    rs.GetFloat(rs.GetOrdinal("z"))));
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not load bookmarks for player: {PlayerId}", playerId);
        }
        return bookmarks;
    }

    public static void StoreBookmark(int playerId, Bookmark bookmark)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = STORE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            stmt.Parameters.Add(new MySqlParameter { Value = bookmark.Name });
            stmt.Parameters.Add(new MySqlParameter { Value = bookmark.WorldId });
            stmt.Parameters.Add(new MySqlParameter { Value = bookmark.X });
            stmt.Parameters.Add(new MySqlParameter { Value = bookmark.Y });
            stmt.Parameters.Add(new MySqlParameter { Value = bookmark.Z });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not add bookmark for player {PlayerId}", playerId);
        }
    }

    public static bool DeleteBookmark(int playerId, string name)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = DELETE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            stmt.Parameters.Add(new MySqlParameter { Value = name });
            return stmt.ExecuteNonQuery() > 0;
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not delete bookmark {BookmarkName} for player {PlayerId}", name, playerId);
            return false;
        }
    }

    public static void DeleteAll(int playerId)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = DELETE_ALL_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not delete all bookmarks");
        }
    }

    public sealed record Bookmark(string Name, int WorldId, float X, float Y, float Z);
}
