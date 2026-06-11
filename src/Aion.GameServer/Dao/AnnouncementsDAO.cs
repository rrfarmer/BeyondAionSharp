using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/AnnouncementsDAO (@author Divinity). Manages announcements. MIXED: loadAnnouncements/delAnnouncement use the commons
/// DB callback helper (DB.Select(ReadStH)/DB.InsertUpdate(IUStH), anonymous->nested handlers); addAnnouncement uses DatabaseFactory.
/// rs.next()/getInt/getString->Read()/GetInt32/GetString(GetOrdinal); message "\\n"/"\\t" unescape preserved. Statement.RETURN_GENERATED_KEYS
/// + getGeneratedKeys().getInt(1) -> ExecuteNonQuery + (int)stmt.LastInsertedId. SQL verbatim.
/// </summary>
public class AnnouncementsDAO
{
    public static List<Announcement> LoadAnnouncements()
    {
        List<Announcement> result = new List<Announcement>();
        DB.Select("SELECT * FROM announcements ORDER BY id", new LoadHandler(result));
        return result;
    }

    private sealed class LoadHandler : ReadStH
    {
        private readonly List<Announcement> result;

        internal LoadHandler(List<Announcement> result)
        {
            this.result = result;
        }

        public void HandleRead(MySqlDataReader resultSet)
        {
            while (resultSet.Read())
            {
                result.Add(GetAnnouncement(resultSet));
            }
        }
    }

    private static Announcement GetAnnouncement(MySqlDataReader resultSet)
    {
        int id = resultSet.GetInt32(resultSet.GetOrdinal("id"));
        string message = resultSet.GetString(resultSet.GetOrdinal("announce")).Replace("\\n", "\n").Replace("\\t", "\t");
        string faction = resultSet.GetString(resultSet.GetOrdinal("faction"));
        string chatType = resultSet.GetString(resultSet.GetOrdinal("type"));
        int delay = resultSet.GetInt32(resultSet.GetOrdinal("delay"));
        return new Announcement(id, message, faction, chatType, delay);
    }

    public static int AddAnnouncement(string message, string faction, string chatType, int delay)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "INSERT INTO announcements (announce, faction, type, delay) VALUES (?, ?, ?, ?)";
            stmt.Parameters.Add(new MySqlParameter { Value = message });
            stmt.Parameters.Add(new MySqlParameter { Value = faction });
            stmt.Parameters.Add(new MySqlParameter { Value = chatType });
            stmt.Parameters.Add(new MySqlParameter { Value = delay });
            stmt.ExecuteNonQuery();
            return (int)stmt.LastInsertedId; // Java: Statement.RETURN_GENERATED_KEYS + getGeneratedKeys().getInt(1)
        }
        catch (Exception e)
        {
            NullLoggerFactory.Instance.CreateLogger(nameof(AnnouncementsDAO)).LogError(e, "");
            return -1;
        }
    }

    public static bool DelAnnouncement(int id)
    {
        return DB.InsertUpdate("DELETE FROM announcements WHERE id = ?", new DelHandler(id));
    }

    private sealed class DelHandler : IUStH
    {
        private readonly int id;

        internal DelHandler(int id)
        {
            this.id = id;
        }

        public void HandleInsertUpdate(MySqlCommand preparedStatement)
        {
            preparedStatement.Parameters.Add(new MySqlParameter { Value = id });
            preparedStatement.ExecuteNonQuery();
        }
    }
}
