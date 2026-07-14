using System;
using System.Collections.Generic;
using System.Data.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Custom.Instance.Neuralnetwork;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/CustomInstancePlayerModelEntryDAO (@author Jo, Estrayl). JDBC DAO over custom_instance_records (neural-network
/// training rows for the custom ranked instance). DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery ->
/// ExecuteReader; rset.getInt/getFloat/getBoolean("col") -> reader.GetX(reader.GetOrdinal("col")); Timestamp reads/writes use SQL epoch
/// milliseconds/FROM_UNIXTIME (PlayerModelEntry uses DateTimeOffset). insertNewRecords: setAutoCommit(false)
/// + addBatch/executeBatch + commit -> MySqlTransaction + MySqlBatch(BatchCommands) + Commit; Persistable.PersistentState ->
/// IPersistable.PersistentState.UPDATED set per row.
/// </summary>
public class CustomInstancePlayerModelEntryDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(CustomInstancePlayerModelEntryDAO));

    private const string SELECT_QUERY = "SELECT *, CAST(FLOOR(UNIX_TIMESTAMP(`timestamp`) * 1000) AS SIGNED) AS `timestamp_epoch_millis` FROM `custom_instance_records` WHERE ? = player_id";
    private const string INSERT_QUERY = "INSERT INTO `custom_instance_records` ( `player_id`, `timestamp`, `skill_id`, `player_class_id`, `player_hp_percentage`,"
        + "`player_mp_percentage`, `player_is_rooted`, `player_is_silenced`, `player_is_bound`, `player_is_stunned`, `player_is_aetherhold`,"
        + "`player_buff_count`, `player_debuff_count`, `player_is_shielded`, `target_hp_percentage`, `target_mp_percentage`,"
        + "`target_focuses_player`, `distance`, `target_is_rooted`, `target_is_silenced`, `target_is_bound`, `target_is_stunned`,"
        + "`target_is_aetherhold`, `target_buff_count`, `target_debuff_count`, `target_is_shielded`) VALUES (?, FROM_UNIXTIME(? / 1000.0), ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, "
        + "?, ?, ?, ?, ?, ?, ?, ?)";

    public static List<PlayerModelEntry> LoadPlayerModelEntries(int playerId)
    {
        List<PlayerModelEntry> entries = new List<PlayerModelEntry>();
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            using MySqlDataReader rset = stmt.ExecuteReader();
            ReadPlayerModelEntries(playerId, rset, entries);
        }
        catch (Exception e)
        {
            log.LogError(e, "[CUSTOM_INSTANCE] Error loading player model entries on player id " + playerId);
        }
        return entries;
    }

    internal static void ReadPlayerModelEntries(int playerId, DbDataReader rset, ICollection<PlayerModelEntry> entries)
    {
        while (rset.Read())
        {
            entries.Add(new PlayerModelEntry(playerId, DatabaseTimestamp.ReadDateTimeOffset(rset, "timestamp_epoch_millis"),
                GetInt32(rset, "skill_id"), GetInt32(rset, "player_class_id"),
                GetFloat(rset, "player_hp_percentage"), GetFloat(rset, "player_mp_percentage"),
                GetBoolean(rset, "player_is_rooted"), GetBoolean(rset, "player_is_silenced"),
                GetBoolean(rset, "player_is_bound"), GetBoolean(rset, "player_is_stunned"),
                GetBoolean(rset, "player_is_aetherhold"), GetInt32(rset, "player_buff_count"),
                GetInt32(rset, "player_debuff_count"), GetBoolean(rset, "player_is_shielded"),
                GetFloat(rset, "target_hp_percentage"), GetFloat(rset, "target_mp_percentage"),
                GetBoolean(rset, "target_focuses_player"), GetFloat(rset, "distance"),
                GetBoolean(rset, "target_is_rooted"), GetBoolean(rset, "target_is_silenced"),
                GetBoolean(rset, "target_is_bound"), GetBoolean(rset, "target_is_stunned"),
                GetBoolean(rset, "target_is_aetherhold"), GetInt32(rset, "target_buff_count"),
                GetInt32(rset, "target_debuff_count"), GetBoolean(rset, "target_is_shielded")));
        }
    }

    private static int GetInt32(DbDataReader rset, string columnName)
    {
        int ordinal = rset.GetOrdinal(columnName);
        return rset.IsDBNull(ordinal) ? 0 : rset.GetInt32(ordinal);
    }

    private static float GetFloat(DbDataReader rset, string columnName)
    {
        int ordinal = rset.GetOrdinal(columnName);
        return rset.IsDBNull(ordinal) ? 0f : rset.GetFloat(ordinal);
    }

    private static bool GetBoolean(DbDataReader rset, string columnName)
    {
        int ordinal = rset.GetOrdinal(columnName);
        return !rset.IsDBNull(ordinal) && rset.GetBoolean(ordinal);
    }

    public static void InsertNewRecords(ICollection<PlayerModelEntry> filteredEntries)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlTransaction transaction = con.BeginTransaction();
            using MySqlBatch batch = new MySqlBatch(con, transaction);
            foreach (PlayerModelEntry pme in filteredEntries)
            {
                MySqlBatchCommand cmd = new MySqlBatchCommand(INSERT_QUERY);
                cmd.Parameters.Add(new MySqlParameter { Value = pme.GetPlayerID() });
                cmd.Parameters.Add(new MySqlParameter { Value = DatabaseTimestamp.ToUnixTimeMilliseconds(pme.GetTimestamp()) });
                cmd.Parameters.Add(new MySqlParameter { Value = pme.GetSkillID() });
                cmd.Parameters.Add(new MySqlParameter { Value = pme.GetPlayerClassID() });
                cmd.Parameters.Add(new MySqlParameter { Value = pme.GetPlayerHPpercentage() });
                cmd.Parameters.Add(new MySqlParameter { Value = pme.GetPlayerMPpercentage() });
                cmd.Parameters.Add(new MySqlParameter { Value = pme.IsPlayerRooted() });
                cmd.Parameters.Add(new MySqlParameter { Value = pme.IsPlayerSilenced() });
                cmd.Parameters.Add(new MySqlParameter { Value = pme.IsPlayerBound() });
                cmd.Parameters.Add(new MySqlParameter { Value = pme.IsPlayerStunned() });
                cmd.Parameters.Add(new MySqlParameter { Value = pme.IsPlayerAetherhold() });
                cmd.Parameters.Add(new MySqlParameter { Value = pme.GetPlayerBuffCount() });
                cmd.Parameters.Add(new MySqlParameter { Value = pme.GetPlayerDebuffCount() });
                cmd.Parameters.Add(new MySqlParameter { Value = pme.IsPlayerIsShielded() });
                cmd.Parameters.Add(new MySqlParameter { Value = pme.GetTargetHPpercentage() });
                cmd.Parameters.Add(new MySqlParameter { Value = pme.GetTargetMPpercentage() });
                cmd.Parameters.Add(new MySqlParameter { Value = pme.IsTargetFocusesPlayer() });
                cmd.Parameters.Add(new MySqlParameter { Value = pme.GetDistance() });
                cmd.Parameters.Add(new MySqlParameter { Value = pme.IsTargetRooted() });
                cmd.Parameters.Add(new MySqlParameter { Value = pme.IsTargetSilenced() });
                cmd.Parameters.Add(new MySqlParameter { Value = pme.IsTargetBound() });
                cmd.Parameters.Add(new MySqlParameter { Value = pme.IsTargetStunned() });
                cmd.Parameters.Add(new MySqlParameter { Value = pme.IsTargetAetherhold() });
                cmd.Parameters.Add(new MySqlParameter { Value = pme.GetTargetBuffCount() });
                cmd.Parameters.Add(new MySqlParameter { Value = pme.GetTargetDebuffCount() });
                cmd.Parameters.Add(new MySqlParameter { Value = pme.IsTargetIsShielded() });
                batch.BatchCommands.Add(cmd);

                pme.SetPersistentState(IPersistable.PersistentState.UPDATED);
            }
            batch.ExecuteNonQuery();
            transaction.Commit();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error occured while saving player model entries.");
        }
    }
}
