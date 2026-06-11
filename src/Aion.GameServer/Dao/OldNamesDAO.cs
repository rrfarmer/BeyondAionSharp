using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/OldNamesDAO (@author synchro2). JDBC DAO over the old_names table for the rename name-reservation rule.
/// DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader, rs.next()+rs.getInt("cnt") ->
/// reader.Read()+reader.GetInt32(reader.GetOrdinal("cnt")); execute -> ExecuteNonQuery; inline SQL (incl. COALESCE/INTERVAL ? DAY)
/// preserved verbatim. SLF4J '{}' placeholders -> C# named message-template placeholders (exception moved to the LogError first arg).
/// </summary>
public class OldNamesDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(OldNamesDAO));

    public static bool IsNameReserved(string oldName, string newName, int nameReservationDurationDays)
    {
        if (nameReservationDurationDays > 0)
        {
            try
            {
                using MySqlConnection con = DatabaseFactory.GetConnection();
                con.Open();
                using MySqlCommand s = con.CreateCommand();
                s.CommandText = "SELECT COUNT(*) cnt FROM old_names WHERE old_name = ? AND COALESCE(new_name != ?, TRUE) AND renamed_date > NOW() - INTERVAL ? DAY";
                s.Parameters.Add(new MySqlParameter { Value = newName });
                s.Parameters.Add(new MySqlParameter { Value = oldName });
                s.Parameters.Add(new MySqlParameter { Value = nameReservationDurationDays });
                using MySqlDataReader rs = s.ExecuteReader();
                rs.Read();
                return rs.GetInt32(rs.GetOrdinal("cnt")) > 0;
            }
            catch (Exception e)
            {
                log.LogError(e, "Couldn't check if name {NewName} is reserved", newName);
            }
        }
        return false;
    }

    public static void InsertNames(int playerId, string oldName, string newName)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "INSERT INTO `old_names` (`player_id`, `old_name`, `new_name`) VALUES (?, ?, ?)";
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            stmt.Parameters.Add(new MySqlParameter { Value = oldName });
            stmt.Parameters.Add(new MySqlParameter { Value = newName });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not insert names for player {PlayerId}: {OldName}>{NewName}", playerId, oldName, newName);
        }
    }
}
