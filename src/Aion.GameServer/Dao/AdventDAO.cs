using System;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/AdventDAO (@author Neon). JDBC DAO over the advent table for the advent-calendar reward gate.
/// java.time.LocalDate -> DateOnly; java.sql.Date.valueOf(date) -> DateOnly param; rs.getDate(name).toLocalDate().isBefore(date)
/// -> DateOnly.FromDateTime(reader.GetDateTime(...)) &lt; date; positional '?' -> ordered MySqlParameter. Inline SQL preserved
/// verbatim (incl. the "? = account_id" ordering and REPLACE INTO). catch (SQLException) -> catch (Exception) (no java.sql analog);
/// both methods swallow the error and return the Java fallback (true / false respectively).
/// </summary>
public class AdventDAO
{
    public static bool CanReceiveReward(Player player, DateOnly date)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "SELECT `last_day_received` FROM `advent` WHERE ? = account_id";
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetAccount().GetId() });
            using MySqlDataReader rs = stmt.ExecuteReader();
            if (!rs.Read())
                return true;
            return DateOnly.FromDateTime(rs.GetDateTime(rs.GetOrdinal("last_day_received"))) < date;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool StoreLastReceivedDay(Player player, DateOnly date)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "REPLACE INTO `advent` VALUES (?, ?)";
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetAccount().GetId() });
            stmt.Parameters.Add(new MySqlParameter { Value = date });
            stmt.ExecuteNonQuery();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
