using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Challenge;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Challenge;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/ChallengeTasksDAO (@author ViAl). JDBC DAO over challenge_tasks (per-owner challenge quest progress).
/// DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; executeUpdate -> ExecuteNonQuery;
/// SQL verbatim. ConcurrentHashMap -> ConcurrentDictionary (containsKey->ContainsKey, putIfAbsent->TryAdd, get->indexer); inner
/// HashMap&lt;Integer,ChallengeQuest&gt;(2) -> Dictionary(2) with indexer puts. ChallengeType.toString()/getType().toString() ->
/// ToString(). Nullable Timestamp reads/writes use SQL epoch milliseconds/FROM_UNIXTIME so ChallengeTask.GetCompleteTime keeps the
/// Java instant without host-local DateTime inference. Persistable.PersistentState -> IPersistable.PersistentState (NEW/UPDATE_REQUIRED).
/// </summary>
public class ChallengeTasksDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(ChallengeTasksDAO));

    private const string SELECT_QUERY = "SELECT *, CAST(FLOOR(UNIX_TIMESTAMP(`complete_time`) * 1000) AS SIGNED) AS `complete_time_epoch_millis` FROM `challenge_tasks` WHERE `owner_id` = ? AND `owner_type` = ?";
    private const string INSERT_QUERY = "INSERT INTO `challenge_tasks` (`task_id`, `quest_id`, `owner_id`, `owner_type`, `complete_count`, `complete_time`) VALUES (?, ?, ?, ?, ?, FROM_UNIXTIME(? / 1000.0));";
    private const string UPDATE_QUERY = "UPDATE `challenge_tasks` SET `complete_count` = ?, `complete_time`= FROM_UNIXTIME(? / 1000.0) WHERE `task_id` = ? AND `quest_id` = ? AND `owner_id` = ?";

    public static Dictionary<int, ChallengeTask> Load(int ownerId, ChallengeType type)
    {
        ConcurrentDictionary<int, ChallengeTask> tasks = new ConcurrentDictionary<int, ChallengeTask>();
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = ownerId });
            stmt.Parameters.Add(new MySqlParameter { Value = type.ToString() });
            using MySqlDataReader rset = stmt.ExecuteReader();
            while (rset.Read())
            {
                int taskId = rset.GetInt32(rset.GetOrdinal("task_id"));
                int questId = rset.GetInt32(rset.GetOrdinal("quest_id"));
                int completeCount = rset.GetInt32(rset.GetOrdinal("complete_count"));
                DateTimeOffset? date = DatabaseTimestamp.ReadNullableDateTimeOffset(rset, "complete_time_epoch_millis");
                ChallengeQuestTemplate template = DataManager.CHALLENGE_DATA.GetQuestByQuestId(questId);
                ChallengeQuest quest = new ChallengeQuest(template, completeCount);
                quest.SetPersistentState(IPersistable.PersistentState.UPDATED);
                if (!tasks.ContainsKey(taskId))
                {
                    Dictionary<int, ChallengeQuest> quests = new Dictionary<int, ChallengeQuest>(2);
                    quests[quest.GetQuestId()] = quest;
                    ChallengeTask task = new ChallengeTask(taskId, ownerId, quests, date);
                    tasks.TryAdd(taskId, task);
                }
                else
                {
                    tasks[taskId].GetQuests()[questId] = quest;
                }
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not load " + type + " challenge tasks of owner " + ownerId);
        }
        return new Dictionary<int, ChallengeTask>(tasks);
    }

    public static void StoreTask(ChallengeTask task)
    {
        foreach (ChallengeQuest quest in task.GetQuests().Values)
        {
            switch (quest.GetPersistentState())
            {
                case IPersistable.PersistentState.NEW:
                    InsertQuestEntry(task, quest);
                    break;
                case IPersistable.PersistentState.UPDATE_REQUIRED:
                    UpdateQuestEntry(task, quest);
                    break;
            }
        }
    }

    private static void InsertQuestEntry(ChallengeTask task, ChallengeQuest quest)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = INSERT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = task.GetTaskId() });
            stmt.Parameters.Add(new MySqlParameter { Value = quest.GetQuestId() });
            stmt.Parameters.Add(new MySqlParameter { Value = task.GetOwnerId() });
            stmt.Parameters.Add(new MySqlParameter { Value = task.GetTemplate().GetType_().ToString() });
            stmt.Parameters.Add(new MySqlParameter { Value = quest.GetCompleteCount() });
            stmt.Parameters.Add(new MySqlParameter { Value = DatabaseTimestamp.ToUnixTimeMillisecondsOrDbNull(task.GetCompleteTime()) });
            stmt.ExecuteNonQuery();
            quest.SetPersistentState(IPersistable.PersistentState.UPDATED);
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not insert challenge task " + task.GetTaskId() + " of owner " + task.GetOwnerId());
        }
    }

    private static void UpdateQuestEntry(ChallengeTask task, ChallengeQuest quest)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = UPDATE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = quest.GetCompleteCount() });
            stmt.Parameters.Add(new MySqlParameter { Value = DatabaseTimestamp.ToUnixTimeMillisecondsOrDbNull(task.GetCompleteTime()) });
            stmt.Parameters.Add(new MySqlParameter { Value = task.GetTaskId() });
            stmt.Parameters.Add(new MySqlParameter { Value = quest.GetQuestId() });
            stmt.Parameters.Add(new MySqlParameter { Value = task.GetOwnerId() });
            stmt.ExecuteNonQuery();
            quest.SetPersistentState(IPersistable.PersistentState.UPDATED);
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not update challenge task " + task.GetTaskId() + " of owner " + task.GetOwnerId());
        }
    }
}
