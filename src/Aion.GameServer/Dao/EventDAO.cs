using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Utils.Time;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/EventDAO (@author Neon). JDBC DAO over the event table (per-event stored buff index/pool/allowed-days).
/// DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; executeUpdate/execute ->
/// ExecuteNonQuery/Batch; SQL verbatim. ServerTime.now().with(MIDNIGHT).withDayOfMonth(1) -> first-of-month-midnight DateTimeOffset
/// from ServerTime.Now(); Timestamp.from(zdt.toInstant()) -> startOfMonth.UtcDateTime. serialize: Set&lt;Integer&gt; -> comma-join (null
/// when null); parseInts: split(",")+Integer.parseInt -> HashSet&lt;int&gt; via Split+int.Parse (null when null). storeBuffData uses
/// addBatch/executeBatch -> MySqlBatch within the connection (delete-then-batch-insert). Java's always-true `if(!rset.isAfterLast())`
/// guard (cursor is before-first on a fresh ResultSet) -> dropped; loadStoredBuffData returns the (possibly empty) list on success, null
/// only on exception, matching Java. Nested static StoredBuffData carrier kept.
/// </summary>
public class EventDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(EventDAO));

    private const string SELECT_QUERY = "SELECT `buff_index`, `buff_active_pool_ids`, `buff_allowed_days` FROM `event` WHERE `event_name`=?";
    private const string INSERT_QUERY = "INSERT INTO `event` (`event_name`, `buff_index`, `buff_active_pool_ids`, `buff_allowed_days`) VALUES (?,?,?,?)";
    private const string DELETE_QUERY = "DELETE FROM `event` WHERE `event_name`=?";
    private const string DELETE_OLD_QUERY = "DELETE FROM `event` WHERE last_change < ?";

    public static void DeleteOldBuffData()
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand deleteStatement = con.CreateCommand();
            deleteStatement.CommandText = DELETE_OLD_QUERY;
            DateTimeOffset now = ServerTime.Now();
            DateTimeOffset startOfMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset);
            deleteStatement.Parameters.Add(new MySqlParameter { Value = startOfMonth.UtcDateTime });
            deleteStatement.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Couldn't clean up old event buff data");
        }
    }

    public static List<StoredBuffData> LoadStoredBuffData(string eventName)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = eventName });
            using MySqlDataReader rset = stmt.ExecuteReader();
            List<StoredBuffData> storedBuffData = new List<StoredBuffData>();
            while (rset.Read())
            {
                int buffIndex = rset.GetInt32(rset.GetOrdinal("buff_index"));
                HashSet<int> activePoolSkillIds = ParseInts(rset.GetString(rset.GetOrdinal("buff_active_pool_ids")));
                HashSet<int> activeRandomDays = ParseInts(rset.GetString(rset.GetOrdinal("buff_allowed_days")));
                storedBuffData.Add(new StoredBuffData(buffIndex, activePoolSkillIds, activeRandomDays));
            }
            return storedBuffData;
        }
        catch (Exception e)
        {
            log.LogError(e, "Couldn't load stored event buff info for event: " + eventName);
        }
        return null;
    }

    public static bool StoreBuffData(string eventName, List<StoredBuffData> storedBuffData)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand delStmt = con.CreateCommand();
            delStmt.CommandText = DELETE_QUERY;
            delStmt.Parameters.Add(new MySqlParameter { Value = eventName });
            delStmt.ExecuteNonQuery(); // delete all old entries first
            using MySqlBatch batch = new MySqlBatch(con);
            foreach (StoredBuffData data in storedBuffData) // store new entries
            {
                MySqlBatchCommand cmd = new MySqlBatchCommand(INSERT_QUERY);
                cmd.Parameters.Add(new MySqlParameter { Value = eventName });
                cmd.Parameters.Add(new MySqlParameter { Value = data.GetBuffIndex() });
                cmd.Parameters.Add(new MySqlParameter { Value = Serialize(data.GetActivePoolSkillIds()) });
                cmd.Parameters.Add(new MySqlParameter { Value = Serialize(data.GetAllowedBuffDays()) });
                batch.BatchCommands.Add(cmd);
            }
            batch.ExecuteNonQuery();
            return true;
        }
        catch (Exception e)
        {
            log.LogError(e, "Couldn't store event buff info for event: " + eventName);
            return false;
        }
    }

    private static string Serialize(HashSet<int> ints)
    {
        return ints == null ? null : string.Join(",", ints.Select(i => i.ToString()));
    }

    private static HashSet<int> ParseInts(string str)
    {
        return str == null ? null : new HashSet<int>(str.Split(',').Select(int.Parse));
    }

    public class StoredBuffData
    {
        private readonly int buffIndex;
        private readonly HashSet<int> activePoolSkillIds;
        private readonly HashSet<int> allowedBuffDays;

        public StoredBuffData(int buffIndex, HashSet<int> activePoolSkillIds, HashSet<int> allowedBuffDays)
        {
            this.buffIndex = buffIndex;
            this.activePoolSkillIds = activePoolSkillIds;
            this.allowedBuffDays = allowedBuffDays;
        }

        public int GetBuffIndex()
        {
            return buffIndex;
        }

        public HashSet<int> GetActivePoolSkillIds()
        {
            return activePoolSkillIds;
        }

        public HashSet<int> GetAllowedBuffDays()
        {
            return allowedBuffDays;
        }
    }
}
