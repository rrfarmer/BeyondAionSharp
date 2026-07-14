using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/PlayerQuestListDAO (@author MrPoke, vlog, Rolandas). JDBC DAO over player_quests.
/// DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; SQL verbatim. getInt+wasNull (reward)
/// -> IsDBNull ? (int?)null : GetInt32; nullable Timestamp columns use SQL epoch projections/FROM_UNIXTIME (QuestState uses
/// DateTime?/int?); SMALLINT null support uses DBNull.Value. QuestStatus.valueOf ->
/// Enum.Parse&lt;QuestStatus&gt;; status.toString() -> GetStatus().ToString(). store: shared connection (setAutoCommit(false)) with each
/// helper running its batch under a MySqlTransaction+Commit (matching Java's per-helper con.commit()); addBatch/executeBatch ->
/// MySqlBatch. stream().filter(Persistable.NEW/CHANGED/DELETED) -> Where(IPersistable.NEW/CHANGED/DELETED) (tolerated red predicate
/// statics). GenericValidator.isBlankOrNull (commons util, not yet ported) referenced faithfully as a tolerated red ref.
/// </summary>
public class PlayerQuestListDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(PlayerQuestListDAO));

    public const string SELECT_QUERY = "SELECT `quest_id`, `status`, `quest_vars`, `flags`, `complete_count`, CAST(FLOOR(UNIX_TIMESTAMP(`next_repeat_time`) * 1000) AS SIGNED) AS `next_repeat_time_epoch_millis`, `reward`, CAST(FLOOR(UNIX_TIMESTAMP(`complete_time`) * 1000) AS SIGNED) AS `complete_time_epoch_millis` FROM `player_quests` WHERE `player_id`=?";
    public const string UPDATE_QUERY = "UPDATE `player_quests` SET `status`=?, `quest_vars`=?, `flags`=?, `complete_count`=?, `next_repeat_time`=FROM_UNIXTIME(? / 1000.0), `reward`=?, `complete_time`=FROM_UNIXTIME(? / 1000.0) WHERE `player_id`=? AND `quest_id`=?";
    public const string DELETE_QUERY = "DELETE FROM `player_quests` WHERE `player_id`=? AND `quest_id`=?";
    public const string INSERT_QUERY = "INSERT INTO `player_quests` (`player_id`, `quest_id`, `status`, `quest_vars`, `flags`, `complete_count`, `next_repeat_time`, `reward`, `complete_time`) VALUES (?,?,?,?,?,?,FROM_UNIXTIME(? / 1000.0),?,FROM_UNIXTIME(? / 1000.0))";

    public static QuestStateList Load(int playerObjId)
    {
        QuestStateList questStateList = new QuestStateList();
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerObjId });
            using MySqlDataReader rset = stmt.ExecuteReader();
            while (rset.Read())
            {
                int questId = rset.GetInt32(rset.GetOrdinal("quest_id"));
                int questVars = rset.GetInt32(rset.GetOrdinal("quest_vars"));
                int flags = rset.GetInt32(rset.GetOrdinal("flags"));
                int completeCount = rset.GetInt32(rset.GetOrdinal("complete_count"));
                DateTime? nextRepeatTime = DatabaseTimestamp.ReadNullableUtcDateTime(rset, "next_repeat_time_epoch_millis");
                int rewardOrd = rset.GetOrdinal("reward");
                int? reward = rset.IsDBNull(rewardOrd) ? (int?)null : rset.GetInt32(rewardOrd);
                DateTime? completeTime = DatabaseTimestamp.ReadNullableUtcDateTime(rset, "complete_time_epoch_millis");
                QuestStatus status = Enum.Parse<QuestStatus>(rset.GetString(rset.GetOrdinal("status")));
                QuestState questState = new QuestState(questId, status, questVars, flags, completeCount, nextRepeatTime, reward, completeTime);
                questState.SetPersistentState(IPersistable.PersistentState.UPDATED);
                questStateList.AddQuest(questId, questState);
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not restore QuestStateList data for player: " + playerObjId + " from DB: " + e.Message);
        }
        return questStateList;
    }

    public static void Store(Player player)
    {
        List<QuestState> qsList = player.GetQuestStateList().GetAllQuestState();
        ISet<int> delQsList = player.GetQuestStateList().GetDeletedQuestIds();
        if (qsList.Count == 0 && delQsList.Count == 0)
            return;

        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();

            DeleteQuest(con, player.GetObjectId(), qsList, delQsList);
            AddQuests(con, player.GetObjectId(), qsList);
            UpdateQuests(con, player.GetObjectId(), qsList);
        }
        catch (Exception e)
        {
            log.LogError(e, "Can't save quests for player " + player.GetObjectId());
        }

        foreach (QuestState qs in qsList)
        {
            qs.SetPersistentState(IPersistable.PersistentState.UPDATED);
        }
    }

    private static void AddQuests(MySqlConnection con, int playerId, ICollection<QuestState> states)
    {
        states = states.Where(x => IPersistable.NEW(x)).ToList();

        if (GenericValidator.IsBlankOrNull(states))
            return;

        try
        {
            using MySqlTransaction tx = con.BeginTransaction();
            using MySqlBatch batch = new MySqlBatch(con, tx);
            foreach (QuestState qs in states)
            {
                MySqlBatchCommand ps = new MySqlBatchCommand(INSERT_QUERY);
                ps.Parameters.Add(new MySqlParameter { Value = playerId });
                ps.Parameters.Add(new MySqlParameter { Value = qs.GetQuestId() });
                ps.Parameters.Add(new MySqlParameter { Value = qs.GetStatus().ToString() });
                ps.Parameters.Add(new MySqlParameter { Value = qs.GetQuestVars().GetQuestVars() });
                ps.Parameters.Add(new MySqlParameter { Value = qs.GetFlags() });
                ps.Parameters.Add(new MySqlParameter { Value = qs.GetCompleteCount() });
                ps.Parameters.Add(new MySqlParameter { Value = DatabaseTimestamp.ToUnixTimeMillisecondsOrDbNull(qs.GetNextRepeatTime()) });
                ps.Parameters.Add(new MySqlParameter { Value = (object)qs.GetRewardGroup() ?? DBNull.Value }); // supports inserting null value
                ps.Parameters.Add(new MySqlParameter { Value = DatabaseTimestamp.ToUnixTimeMillisecondsOrDbNull(qs.GetLastCompleteTime()) });
                batch.BatchCommands.Add(ps);
            }

            batch.ExecuteNonQuery();
            tx.Commit();
        }
        catch (Exception)
        {
            log.LogError("Failed to insert new quests for player " + playerId);
        }
    }

    private static void UpdateQuests(MySqlConnection con, int playerId, ICollection<QuestState> states)
    {
        states = states.Where(x => IPersistable.CHANGED(x)).ToList();

        if (GenericValidator.IsBlankOrNull(states))
            return;

        try
        {
            using MySqlTransaction tx = con.BeginTransaction();
            using MySqlBatch batch = new MySqlBatch(con, tx);
            foreach (QuestState qs in states)
            {
                MySqlBatchCommand ps = new MySqlBatchCommand(UPDATE_QUERY);
                ps.Parameters.Add(new MySqlParameter { Value = qs.GetStatus().ToString() });
                ps.Parameters.Add(new MySqlParameter { Value = qs.GetQuestVars().GetQuestVars() });
                ps.Parameters.Add(new MySqlParameter { Value = qs.GetFlags() });
                ps.Parameters.Add(new MySqlParameter { Value = qs.GetCompleteCount() });
                ps.Parameters.Add(new MySqlParameter { Value = DatabaseTimestamp.ToUnixTimeMillisecondsOrDbNull(qs.GetNextRepeatTime()) });
                ps.Parameters.Add(new MySqlParameter { Value = (object)qs.GetRewardGroup() ?? DBNull.Value }); // supports inserting null value
                ps.Parameters.Add(new MySqlParameter { Value = DatabaseTimestamp.ToUnixTimeMillisecondsOrDbNull(qs.GetLastCompleteTime()) });
                ps.Parameters.Add(new MySqlParameter { Value = playerId });
                ps.Parameters.Add(new MySqlParameter { Value = qs.GetQuestId() });
                batch.BatchCommands.Add(ps);
            }

            batch.ExecuteNonQuery();
            tx.Commit();
        }
        catch (Exception)
        {
            log.LogError("Failed to update existing quests for player " + playerId);
        }
    }

    private static void DeleteQuest(MySqlConnection con, int playerId, ICollection<QuestState> states, ISet<int> questIds)
    {
        states = states.Where(x => IPersistable.DELETED(x)).ToList();

        if (GenericValidator.IsBlankOrNull(states) && questIds.Count == 0)
            return;

        try
        {
            using MySqlTransaction tx = con.BeginTransaction();
            using MySqlBatch batch = new MySqlBatch(con, tx);
            foreach (QuestState qs in states)
            {
                MySqlBatchCommand ps = new MySqlBatchCommand(DELETE_QUERY);
                ps.Parameters.Add(new MySqlParameter { Value = playerId });
                ps.Parameters.Add(new MySqlParameter { Value = qs.GetQuestId() });
                batch.BatchCommands.Add(ps);
            }

            foreach (int questId in questIds)
            {
                MySqlBatchCommand ps = new MySqlBatchCommand(DELETE_QUERY);
                ps.Parameters.Add(new MySqlParameter { Value = playerId });
                ps.Parameters.Add(new MySqlParameter { Value = questId });
                batch.BatchCommands.Add(ps);
            }

            batch.ExecuteNonQuery();
            tx.Commit();
        }
        catch (Exception)
        {
            log.LogError("Failed to delete existing quests for player " + playerId);
        }
        questIds.Clear();
    }
}
