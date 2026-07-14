using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Team.Legion;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/LegionDAO (@author Simple, cura). JDBC DAO over legions + announcement/emblem/history tables. MIXED across all three
/// DB-access styles (low-level DB.PrepareStatement/Close/ExecuteUpdateAndClose, DB callback IUStH/ParamReadStH->nested, DatabaseFactory).
/// Legion[1]/Legion[] capture trick -> nested handler with Result field. getShort->GetInt16; LegionEmblemType/LegionHistoryAction.valueOf->
/// Enum.Parse, getEmblemType().toString()->ToString(); action.getType()->GetType_(); EnumMap<Type,..>->Dictionary, Type.values()->Enum.GetValues.
/// getTimestamp(col).getTime()/1000->SQL epoch milliseconds/1000; new Timestamp(millis)->FROM_UNIXTIME(epoch);
/// RETURN_GENERATED_KEYS+getGeneratedKeys->LastInsertedId. deleteHistory dynamic %s placeholders ->
/// string.Join("?")+Replace("%s",...). Legion.Announcement record(Message, DateTime Time); LegionHistoryEntry.Id property. SQL verbatim.
/// </summary>
public class LegionDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(LegionDAO));

    private const string INSERT_LEGION_QUERY = "INSERT INTO legions(id, `name`) VALUES (?, ?)";
    private const string SELECT_LEGION_QUERY1 = "SELECT * FROM legions WHERE id=?";
    private const string SELECT_LEGION_QUERY2 = "SELECT * FROM legions WHERE name=?";
    private const string DELETE_LEGION_QUERY = "DELETE FROM legions WHERE id = ?";
    private const string UPDATE_LEGION_QUERY = "UPDATE legions SET name=?, level=?, contribution_points=?, deputy_permission=?, centurion_permission=?, legionary_permission=?, volunteer_permission=?, disband_time=?, occupied_legion_dominion=?, last_legion_dominion=?, current_legion_dominion=? WHERE id=?";
    private const string INSERT_ANNOUNCEMENT_QUERY = "INSERT INTO legion_announcement_list(`legion_id`, `announcement`, `date`) VALUES (?, ?, FROM_UNIXTIME(? / 1000.0))";
    private const string SELECT_ANNOUNCEMENT_QUERY = "SELECT *, CAST(FLOOR(UNIX_TIMESTAMP(`date`) * 1000) AS SIGNED) AS `date_epoch_millis` FROM legion_announcement_list WHERE legion_id = ? ORDER BY date DESC LIMIT 1";
    private const string DELETE_ANNOUNCEMENT_QUERY = "DELETE FROM legion_announcement_list WHERE legion_id = ?";
    private const string INSERT_EMBLEM_QUERY = "INSERT INTO legion_emblems(legion_id, emblem_id, color_a, color_r, color_g, color_b, emblem_type, emblem_data) VALUES (?, ?, ?, ?, ?, ?, ?, ?)";
    private const string UPDATE_EMBLEM_QUERY = "UPDATE legion_emblems SET emblem_id=?, color_a=?, color_r=?, color_g=?, color_b=?, emblem_type=?, emblem_data=? WHERE legion_id=?";
    private const string SELECT_EMBLEM_QUERY = "SELECT * FROM legion_emblems WHERE legion_id=?";
    private const string INSERT_HISTORY_QUERY = "INSERT INTO legion_history(`legion_id`, `date`, `history_type`, `name`, `description`) VALUES (?, FROM_UNIXTIME(? / 1000.0), ?, ?, ?)";
    private const string SELECT_HISTORY_QUERY = "SELECT *, CAST(FLOOR(UNIX_TIMESTAMP(`date`) * 1000) AS SIGNED) AS `date_epoch_millis` FROM `legion_history` WHERE legion_id=? ORDER BY date DESC, id DESC";
    private const string DELETE_HISTORY_QUERY = "DELETE FROM `legion_history` WHERE id IN (%s)";

    public static bool IsNameUsed(string name)
    {
        MySqlCommand s = DB.PrepareStatement("SELECT count(id) as cnt FROM legions WHERE ? = legions.name");
        try
        {
            s.Parameters.Add(new MySqlParameter { Value = name });
            using MySqlDataReader rs = s.ExecuteReader();
            rs.Read();
            return rs.GetInt32(rs.GetOrdinal("cnt")) > 0;
        }
        catch (Exception e)
        {
            log.LogError(e, "Can't check if name " + name + ", is used, returning possitive result");
            return true;
        }
        finally
        {
            DB.Close(s);
        }
    }

    public static bool SaveNewLegion(Legion legion)
    {
        bool success = DB.InsertUpdate(INSERT_LEGION_QUERY, new SaveNewLegionHandler(legion));
        return success;
    }

    private sealed class SaveNewLegionHandler : IUStH
    {
        private readonly Legion legion;

        internal SaveNewLegionHandler(Legion legion)
        {
            this.legion = legion;
        }

        public void HandleInsertUpdate(MySqlCommand preparedStatement)
        {
            preparedStatement.Parameters.Add(new MySqlParameter { Value = legion.GetLegionId() });
            preparedStatement.Parameters.Add(new MySqlParameter { Value = legion.GetName() });
            preparedStatement.ExecuteNonQuery();
        }
    }

    public static void StoreLegion(Legion legion)
    {
        DB.InsertUpdate(UPDATE_LEGION_QUERY, new StoreLegionHandler(legion));
    }

    private sealed class StoreLegionHandler : IUStH
    {
        private readonly Legion legion;

        internal StoreLegionHandler(Legion legion)
        {
            this.legion = legion;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = legion.GetName() });
            stmt.Parameters.Add(new MySqlParameter { Value = legion.GetLegionLevel() });
            stmt.Parameters.Add(new MySqlParameter { Value = legion.GetContributionPoints() });
            stmt.Parameters.Add(new MySqlParameter { Value = legion.GetDeputyPermission() });
            stmt.Parameters.Add(new MySqlParameter { Value = legion.GetCenturionPermission() });
            stmt.Parameters.Add(new MySqlParameter { Value = legion.GetLegionaryPermission() });
            stmt.Parameters.Add(new MySqlParameter { Value = legion.GetVolunteerPermission() });
            stmt.Parameters.Add(new MySqlParameter { Value = legion.GetDisbandTime() });
            stmt.Parameters.Add(new MySqlParameter { Value = legion.GetOccupiedLegionDominion() });
            stmt.Parameters.Add(new MySqlParameter { Value = legion.GetLastLegionDominion() });
            stmt.Parameters.Add(new MySqlParameter { Value = legion.GetCurrentLegionDominion() });
            stmt.Parameters.Add(new MySqlParameter { Value = legion.GetLegionId() });
            stmt.ExecuteNonQuery();
        }
    }

    public static Legion LoadLegion(string legionName)
    {
        LoadLegionHandler handler = new LoadLegionHandler(legionName, null);
        bool success = DB.Select(SELECT_LEGION_QUERY2, handler);
        return success ? handler.Result : null;
    }

    public static Legion LoadLegion(int legionId)
    {
        LoadLegionHandler handler = new LoadLegionHandler(legionId, legionId);
        bool success = DB.Select(SELECT_LEGION_QUERY1, handler);
        return success ? handler.Result : null;
    }

    private sealed class LoadLegionHandler : ParamReadStH
    {
        private readonly object paramValue;
        private readonly int? legionIdForCtor;
        internal Legion Result;

        internal LoadLegionHandler(object paramValue, int? legionIdForCtor)
        {
            this.paramValue = paramValue;
            this.legionIdForCtor = legionIdForCtor;
        }

        public void SetParams(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = paramValue });
        }

        public void HandleRead(MySqlDataReader resultSet)
        {
            if (resultSet.Read())
            {
                int legionId = legionIdForCtor ?? resultSet.GetInt32(resultSet.GetOrdinal("id"));
                Result = new Legion(legionId, resultSet.GetString(resultSet.GetOrdinal("name")));
                Result.SetLegionLevel(resultSet.GetInt32(resultSet.GetOrdinal("level")));
                Result.AddContributionPoints(resultSet.GetInt64(resultSet.GetOrdinal("contribution_points")));

                Result.SetLegionPermissions(resultSet.GetInt16(resultSet.GetOrdinal("deputy_permission")), resultSet.GetInt16(resultSet.GetOrdinal("centurion_permission")),
                    resultSet.GetInt16(resultSet.GetOrdinal("legionary_permission")), resultSet.GetInt16(resultSet.GetOrdinal("volunteer_permission")));

                Result.SetDisbandTime(resultSet.GetInt32(resultSet.GetOrdinal("disband_time")));
                Result.SetOccupiedLegionDominion(resultSet.GetInt32(resultSet.GetOrdinal("occupied_legion_dominion")));
                Result.SetLastLegionDominion(resultSet.GetInt32(resultSet.GetOrdinal("last_legion_dominion")));
                Result.SetCurrentLegionDominion(resultSet.GetInt32(resultSet.GetOrdinal("current_legion_dominion")));
            }
        }
    }

    public static void DeleteLegion(int legionId)
    {
        MySqlCommand statement = DB.PrepareStatement(DELETE_LEGION_QUERY);
        try
        {
            statement.Parameters.Add(new MySqlParameter { Value = legionId });
        }
        catch (Exception e)
        {
            log.LogError(e, "deleteLegion #1");
        }
        DB.ExecuteUpdateAndClose(statement);
    }

    public static int[] GetUsedIDs()
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "SELECT id FROM legions";
            using MySqlDataReader rs = stmt.ExecuteReader();
            List<int> ids = new List<int>();
            while (rs.Read())
                ids.Add(rs.GetInt32(rs.GetOrdinal("id")));
            return ids.ToArray();
        }
        catch (Exception e)
        {
            log.LogError(e, "Can't get list of IDs from legions table");
            return null;
        }
    }

    public static Legion.Announcement LoadAnnouncement(int legionId)
    {
        Legion.Announcement announcement = null;
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_ANNOUNCEMENT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = legionId });
            using MySqlDataReader resultSet = stmt.ExecuteReader();
            if (resultSet.Read())
            {
                string message = resultSet.GetString(resultSet.GetOrdinal("announcement"));
                DateTime date = DatabaseTimestamp.ReadUtcDateTime(resultSet, "date_epoch_millis");
                announcement = new Legion.Announcement(message, date);
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Couldn't load legion announcements for legion " + legionId);
        }
        return announcement;
    }

    public static void SaveAnnouncement(int legionId, Legion.Announcement announcement)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using (MySqlCommand delete = con.CreateCommand())
            {
                delete.CommandText = DELETE_ANNOUNCEMENT_QUERY;
                delete.Parameters.Add(new MySqlParameter { Value = legionId });
                delete.ExecuteNonQuery();
            }
            if (announcement != null)
            {
                using MySqlCommand insert = con.CreateCommand();
                insert.CommandText = INSERT_ANNOUNCEMENT_QUERY;
                insert.Parameters.Add(new MySqlParameter { Value = legionId });
                insert.Parameters.Add(new MySqlParameter { Value = announcement.Message });
                insert.Parameters.Add(new MySqlParameter { Value = DatabaseTimestamp.ToUnixTimeMilliseconds(announcement.Time) });
                insert.ExecuteNonQuery();
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Couldn't save announcement for legion " + legionId + ": " + announcement);
        }
    }

    public static void StoreLegionEmblem(int legionId, LegionEmblem legionEmblem)
    {
        if (!ValidEmblem(legionEmblem))
            return;
        if (!(CheckEmblem(legionId)))
            CreateLegionEmblem(legionId, legionEmblem);
        else
        {
            switch (legionEmblem.GetPersistentState())
            {
                case IPersistable.PersistentState.UPDATE_REQUIRED:
                    UpdateLegionEmblem(legionId, legionEmblem);
                    break;
                case IPersistable.PersistentState.NEW:
                    CreateLegionEmblem(legionId, legionEmblem);
                    break;
            }
        }
        legionEmblem.SetPersistentState(IPersistable.PersistentState.UPDATED);
    }

    private static bool ValidEmblem(LegionEmblem legionEmblem)
    {
        return legionEmblem.GetEmblemType() != LegionEmblemType.CUSTOM || legionEmblem.GetCustomEmblemData() != null;
    }

    public static bool CheckEmblem(int legionid)
    {
        MySqlCommand st = DB.PrepareStatement(SELECT_EMBLEM_QUERY);
        try
        {
            st.Parameters.Add(new MySqlParameter { Value = legionid });

            using MySqlDataReader rs = st.ExecuteReader();

            if (rs.Read())
                return true;
        }
        catch (Exception e)
        {
            log.LogError(e, "Can't check " + legionid + " legion emblem: ");
        }
        finally
        {
            DB.Close(st);
        }
        return false;
    }

    private static void CreateLegionEmblem(int legionId, LegionEmblem legionEmblem)
    {
        DB.InsertUpdate(INSERT_EMBLEM_QUERY, new CreateEmblemHandler(legionId, legionEmblem));
    }

    private sealed class CreateEmblemHandler : IUStH
    {
        private readonly int legionId;
        private readonly LegionEmblem legionEmblem;

        internal CreateEmblemHandler(int legionId, LegionEmblem legionEmblem)
        {
            this.legionId = legionId;
            this.legionEmblem = legionEmblem;
        }

        public void HandleInsertUpdate(MySqlCommand preparedStatement)
        {
            preparedStatement.Parameters.Add(new MySqlParameter { Value = legionId });
            preparedStatement.Parameters.Add(new MySqlParameter { Value = legionEmblem.GetEmblemId() });
            preparedStatement.Parameters.Add(new MySqlParameter { Value = legionEmblem.GetColor_a() });
            preparedStatement.Parameters.Add(new MySqlParameter { Value = legionEmblem.GetColor_r() });
            preparedStatement.Parameters.Add(new MySqlParameter { Value = legionEmblem.GetColor_g() });
            preparedStatement.Parameters.Add(new MySqlParameter { Value = legionEmblem.GetColor_b() });
            preparedStatement.Parameters.Add(new MySqlParameter { Value = legionEmblem.GetEmblemType().ToString() });
            preparedStatement.Parameters.Add(new MySqlParameter { Value = legionEmblem.GetCustomEmblemData() });
            preparedStatement.ExecuteNonQuery();
        }
    }

    private static void UpdateLegionEmblem(int legionId, LegionEmblem legionEmblem)
    {
        DB.InsertUpdate(UPDATE_EMBLEM_QUERY, new UpdateEmblemHandler(legionId, legionEmblem));
    }

    private sealed class UpdateEmblemHandler : IUStH
    {
        private readonly int legionId;
        private readonly LegionEmblem legionEmblem;

        internal UpdateEmblemHandler(int legionId, LegionEmblem legionEmblem)
        {
            this.legionId = legionId;
            this.legionEmblem = legionEmblem;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = legionEmblem.GetEmblemId() });
            stmt.Parameters.Add(new MySqlParameter { Value = legionEmblem.GetColor_a() });
            stmt.Parameters.Add(new MySqlParameter { Value = legionEmblem.GetColor_r() });
            stmt.Parameters.Add(new MySqlParameter { Value = legionEmblem.GetColor_g() });
            stmt.Parameters.Add(new MySqlParameter { Value = legionEmblem.GetColor_b() });
            stmt.Parameters.Add(new MySqlParameter { Value = legionEmblem.GetEmblemType().ToString() });
            stmt.Parameters.Add(new MySqlParameter { Value = legionEmblem.GetCustomEmblemData() });
            stmt.Parameters.Add(new MySqlParameter { Value = legionId });
            stmt.ExecuteNonQuery();
        }
    }

    public static LegionEmblem LoadLegionEmblem(int legionId)
    {
        LegionEmblem legionEmblem = new LegionEmblem();

        DB.Select(SELECT_EMBLEM_QUERY, new LoadEmblemHandler(legionId, legionEmblem));
        legionEmblem.SetPersistentState(IPersistable.PersistentState.UPDATED);

        return legionEmblem;
    }

    private sealed class LoadEmblemHandler : ParamReadStH
    {
        private readonly int legionId;
        private readonly LegionEmblem legionEmblem;

        internal LoadEmblemHandler(int legionId, LegionEmblem legionEmblem)
        {
            this.legionId = legionId;
            this.legionEmblem = legionEmblem;
        }

        public void SetParams(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = legionId });
        }

        public void HandleRead(MySqlDataReader resultSet)
        {
            while (resultSet.Read())
            {
                legionEmblem.SetEmblem(resultSet.GetByte(resultSet.GetOrdinal("emblem_id")), resultSet.GetByte(resultSet.GetOrdinal("color_a")), resultSet.GetByte(resultSet.GetOrdinal("color_r")),
                    resultSet.GetByte(resultSet.GetOrdinal("color_g")), resultSet.GetByte(resultSet.GetOrdinal("color_b")), Enum.Parse<LegionEmblemType>(resultSet.GetString(resultSet.GetOrdinal("emblem_type"))),
                    resultSet.GetFieldValue<byte[]>(resultSet.GetOrdinal("emblem_data")));
            }
        }
    }

    public static void LoadHistory(Legion legion)
    {
        Dictionary<LegionHistoryAction.Type, List<LegionHistoryEntry>> history = new Dictionary<LegionHistoryAction.Type, List<LegionHistoryEntry>>();
        foreach (LegionHistoryAction.Type type in Enum.GetValues(typeof(LegionHistoryAction.Type)))
            history[type] = new List<LegionHistoryEntry>();
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_HISTORY_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = legion.GetLegionId() });
            using MySqlDataReader resultSet = stmt.ExecuteReader();
            while (resultSet.Read())
            {
                int id = resultSet.GetInt32(resultSet.GetOrdinal("id"));
                int epochSeconds = unchecked((int)(resultSet.GetInt64(resultSet.GetOrdinal("date_epoch_millis")) / 1000));
                LegionHistoryAction action = LegionHistoryAction.ValueOf(resultSet.GetString(resultSet.GetOrdinal("history_type")));
                string name = resultSet.GetString(resultSet.GetOrdinal("name"));
                string description = resultSet.GetString(resultSet.GetOrdinal("description"));
                history[action.GetType_()].Add(new LegionHistoryEntry(id, epochSeconds, action, name, description));
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not load history of legion " + legion);
        }
        legion.SetHistory(history);
    }

    public static LegionHistoryEntry InsertHistory(int legionId, LegionHistoryAction action, string name, string description)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = INSERT_HISTORY_QUERY;
            long nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            stmt.Parameters.Add(new MySqlParameter { Value = legionId });
            stmt.Parameters.Add(new MySqlParameter { Value = nowMillis });
            stmt.Parameters.Add(new MySqlParameter { Value = action.ToString() });
            stmt.Parameters.Add(new MySqlParameter { Value = name });
            stmt.Parameters.Add(new MySqlParameter { Value = description });
            stmt.ExecuteNonQuery();
            return new LegionHistoryEntry((int)stmt.LastInsertedId, unchecked((int)(nowMillis / 1000)), action, name, description);
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not add history entry for legion " + legionId);
            return null;
        }
    }

    public static void DeleteHistory(int legionId, List<LegionHistoryEntry> entries)
    {
        if (entries.Count == 0)
            return;
        string placeholders = string.Join(",", Enumerable.Repeat("?", entries.Count));
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = DELETE_HISTORY_QUERY.Replace("%s", placeholders);
            for (int i = 0; i < entries.Count; i++)
                stmt.Parameters.Add(new MySqlParameter { Value = entries[i].Id });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not delete " + entries.Count + " history entries for legion " + legionId);
        }
    }
}
