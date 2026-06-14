using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Challenge;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Challenge;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Model.Town;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Mail;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Extensions;

namespace Aion.GameServer.Services;

/// <summary>
/// Java parity: services/ChallengeTaskService (@author ViAl). Singleton getInstance(); ConcurrentHashMap -> ConcurrentDictionary;
/// computeIfAbsent -> GetOrAdd; HashMap inner maps -> Dictionary. SM_SYSTEM_MESSAGE/PlayerService(Players ns)/TownService faithful.
/// town.getL10n()/template.getL10n() via IL10n extension. TreeMap descendingMap -> SortedDictionary OrderByDescending. LetterType.NORMAL.
/// </summary>
public class ChallengeTaskService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(ChallengeTaskService));

    private readonly ConcurrentDictionary<int, Dictionary<int, int>> taskAcceptTownIds;
    private readonly ConcurrentDictionary<int, Dictionary<int, ChallengeTask>> cityTasks;
    private readonly ConcurrentDictionary<int, Dictionary<int, ChallengeTask>> legionTasks;

    private static class SingletonHolder
    {
        internal static readonly ChallengeTaskService instance = new ChallengeTaskService();
    }

    public static ChallengeTaskService GetInstance()
    {
        return SingletonHolder.instance;
    }

    private ChallengeTaskService()
    {
        taskAcceptTownIds = new ConcurrentDictionary<int, Dictionary<int, int>>();
        cityTasks = new ConcurrentDictionary<int, Dictionary<int, ChallengeTask>>();
        legionTasks = new ConcurrentDictionary<int, Dictionary<int, ChallengeTask>>();
        log.LogInformation("ChallengeTaskService initialized.");
    }

    public void ShowTaskList(Player player, ChallengeType challengeType, int ownerId)
    {
        if (CustomConfig.CHALLENGE_TASKS_ENABLED)
        {
            int ownerLevel = 0;
            switch (challengeType)
            {
                case ChallengeType.TOWN:
                    ownerLevel = TownService.GetInstance().GetTownById(ownerId).GetLevel();
                    break;
                case ChallengeType.LEGION:
                    ownerLevel = player.GetLegion().GetLegionLevel();
                    break;
            }
            List<ChallengeTask> availableTasks = BuildTaskList(player, challengeType, ownerId, ownerLevel);
            PacketSendUtility.SendPacket(player, new SM_CHALLENGE_LIST(2, ownerId, challengeType, availableTasks));
            foreach (ChallengeTask task in availableTasks)
            {
                PacketSendUtility.SendPacket(player, new SM_CHALLENGE_LIST(7, ownerId, challengeType, task));
            }
        }
    }

    private List<ChallengeTask> BuildTaskList(Player player, ChallengeType challengeType, int ownerId, int ownerLevel)
    {
        ConcurrentDictionary<int, Dictionary<int, ChallengeTask>> taskMap;
        if (challengeType == ChallengeType.LEGION)
            taskMap = legionTasks;
        else
            taskMap = cityTasks;
        List<ChallengeTask> availableTasks = new List<ChallengeTask>();
        if (!taskMap.ContainsKey(ownerId))
        {
            Dictionary<int, ChallengeTask> tasks = ChallengeTasksDAO.Load(ownerId, challengeType);
            taskMap[ownerId] = tasks;
        }
        foreach (ChallengeTask ct in taskMap[ownerId].Values)
        {
            if (ct.GetTemplate().IsRepeatable() || !ct.IsCompleted())
                availableTasks.Add(ct);
        }
        foreach (ChallengeTaskTemplate template in DataManager.CHALLENGE_DATA.GetTasks().Values)
        {
            if (template.GetType_() == challengeType && template.GetRace() == player.GetRace())
            {
                if (!taskMap[ownerId].ContainsKey(template.GetId()))
                {
                    if (ownerLevel >= template.GetMinLevel() && ownerLevel <= template.GetMaxLevel())
                    {
                        if (template.GetPrevTask() == null)
                        {
                            ChallengeTask task = new ChallengeTask(ownerId, template);
                            taskMap[ownerId][task.GetTaskId()] = task;
                            ChallengeTasksDAO.StoreTask(task);
                            availableTasks.Add(task);
                        }
                        else
                        {
                            int prevTaskId = template.GetPrevTask().Value;
                            if (taskMap[ownerId].ContainsKey(prevTaskId))
                            {
                                ChallengeTask prevTask = taskMap[ownerId][prevTaskId];
                                if (prevTask.IsCompleted())
                                {
                                    ChallengeTask task = new ChallengeTask(ownerId, template);
                                    taskMap[ownerId][task.GetTaskId()] = task;
                                    ChallengeTasksDAO.StoreTask(task);
                                    availableTasks.Add(task);
                                }
                            }
                        }
                    }
                }
            }
        }
        return availableTasks;
    }

    public void OnChallengeQuestFinish(Player player, int questId)
    {
        ChallengeTaskTemplate taskTemplate = DataManager.CHALLENGE_DATA.GetTaskByQuestId(questId);
        switch (taskTemplate.GetType_())
        {
            case ChallengeType.TOWN:
                OnCityTaskFinish(player, taskTemplate, questId);
                break;
            case ChallengeType.LEGION:
                OnLegionTaskFinish(player, taskTemplate, questId);
                break;
        }
    }

    public void OnAcceptTask(Player player, int questId)
    {
        int townId = TownService.GetInstance().GetTownIdByPosition(player);
        if (townId != 0)
            taskAcceptTownIds.GetOrAdd(player.GetObjectId(), _ => new Dictionary<int, int>())[questId] = townId;
    }

    private void OnCityTaskFinish(Player player, ChallengeTaskTemplate taskTemplate, int questId)
    {
        Dictionary<int, int> townsByQuestId = taskAcceptTownIds.TryGetValue(player.GetObjectId(), out var m) ? m : null;
        int? townId = null;
        if (townsByQuestId != null && townsByQuestId.TryGetValue(questId, out var tid))
        {
            townId = tid;
            townsByQuestId.Remove(questId);
        }
        if (townId == null) // server got restarted after player accepted the quest or quest got started outside town (by chat command)
            return;
        ChallengeTask task = GetChallengeTask(player, taskTemplate, townId.Value);
        if (task == null) // task may be not available anymore due to town levelup
            return;
        ChallengeQuest quest = task.GetQuest(questId);
        if (quest == null)
        {
            log.LogWarning(player + " finished city task " + task.GetTaskId() + " of town " + townId + " but info for quest " + questId + " is missing.");
            return;
        }
        if (quest.GetCompleteCount() < quest.GetMaxRepeats() && !task.IsCompleted())
        {
            task.UpdateCompleteTime();
            quest.IncreaseCompleteCount();
            ChallengeTasksDAO.StoreTask(task);
            Town town = TownService.GetInstance().GetTownById(townId.Value);
            if (town != null)
            {
                int oldLevel = town.GetLevel();
                town.IncreasePoints(quest.GetScorePerQuest());
                if (task.IsCompleted())
                {
                    switch (taskTemplate.GetReward().GetType_())
                    {
                        case RewardType.POINT:
                            town.IncreasePoints(taskTemplate.GetReward().GetValue().Value);
                            break;
                        case RewardType.SPAWN:
                            // TODO
                            break;
                    }
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_TOWN_MISSION_COMPLETE(town.GetL10n(), task.GetTemplate().GetL10n()));
                }
                if (town.GetLevel() != oldLevel)
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_TOWN_LEVEL_LEVEL_UP(town.GetL10n(), town.GetLevel()));
                TownDAO.Store(town);
            }
        }
    }

    private ChallengeTask GetChallengeTask(Player player, ChallengeTaskTemplate taskTemplate, int townId)
    {
        Dictionary<int, ChallengeTask> taskMap = cityTasks.TryGetValue(townId, out var tm) ? tm : null;
        if (taskMap == null)
        {
            BuildTaskList(player, ChallengeType.TOWN, townId, TownService.GetInstance().GetTownById(townId).GetLevel());
            taskMap = cityTasks.TryGetValue(townId, out var tm2) ? tm2 : null;
            if (taskMap == null)
            {
                log.LogWarning("Town " + townId + " has no CityTasks! " + player + ", town residence:" + TownService.GetInstance().GetTownResidence(player));
                return null;
            }
        }
        return taskMap.TryGetValue(taskTemplate.GetId(), out var t) ? t : null;
    }

    private void OnLegionTaskFinish(Player player, ChallengeTaskTemplate taskTemplate, int questId)
    {
        // Player could take challenge task and after that leave legion.
        if (player.GetLegion() == null)
            return;
        int legionId = player.GetLegion().GetLegionId();
        // If player took challenge task in one legion, then leave that legion and enter another.
        if (!legionTasks.ContainsKey(legionId))
            return;
        // If player took challenge task in one legion, then leave that legion and enter another, and after that completed this task in new legion.
        if (!legionTasks[legionId].TryGetValue(taskTemplate.GetId(), out var existing) || existing == null)
            return;
        ChallengeTask task = legionTasks[player.GetLegion().GetLegionId()][taskTemplate.GetId()];
        ChallengeQuest quest = task.GetQuests()[questId];
        if (quest.GetCompleteCount() >= quest.GetMaxRepeats())
            return;
        player.GetLegionMember().IncreaseChallengeScore(quest.GetScorePerQuest());
        if (!task.IsCompleted())
        {
            task.UpdateCompleteTime();
            quest.IncreaseCompleteCount();
            foreach (Player p in player.GetLegion().GetOnlinePlayers())
                ShowTaskList(p, ChallengeType.LEGION, legionId);
            ChallengeTasksDAO.StoreTask(task);
            if (task.IsCompleted())
            {
                SortedDictionary<int, List<int>> winnersByPoints = new SortedDictionary<int, List<int>>();
                foreach (LegionMember legionMember in player.GetLegion().GetMembers())
                {
                    if (!winnersByPoints.TryGetValue(legionMember.GetChallengeScore(), out var bucket))
                    {
                        bucket = new List<int>();
                        winnersByPoints[legionMember.GetChallengeScore()] = bucket;
                    }
                    bucket.Add(legionMember.GetObjectId());
                    legionMember.SetChallengeScore(0);
                    if (!legionMember.IsOnline()) // save legionMember to DB since owning player is not online (no autosave schedule)
                        LegionService.GetInstance().StoreLegionMember(legionMember);
                }
                int rewardsAdded = 0, itemId, itemCount;
                foreach (var e in winnersByPoints.OrderByDescending(kv => kv.Key))
                {
                    foreach (int objectId in e.Value)
                    {
                        foreach (ContributionReward reward in taskTemplate.GetContrib())
                        {
                            if (rewardsAdded <= reward.GetNumber())
                            {
                                rewardsAdded++;
                                itemId = reward.GetRewardId();
                                itemCount = reward.GetItemCount();
                                string recipientName = PlayerService.GetPlayerName(objectId);
                                SystemMailService.SendMail("Legion reward", recipientName, "", "", itemId, itemCount, 0, LetterType.NORMAL);
                                break;
                            }
                        }
                    }
                    e.Value.Clear();
                }
            }
        }
    }

    public bool CanRaiseLegionLevel(Legion legion, Player actingPlayer)
    {
        Dictionary<int, ChallengeTask> tasks = legionTasks.GetOrAdd(legion.GetLegionId(),
            id => ChallengeTasksDAO.Load(id, ChallengeType.LEGION));
        List<ChallengeTask> requiredTasksForLevel = new List<ChallengeTask>();
        foreach (ChallengeTask task in tasks.Values)
        {
            ChallengeTaskTemplate taskTemplate = task.GetTemplate();
            if (taskTemplate.IsLegionLevelTask() && taskTemplate.GetMinLevel() == legion.GetLegionLevel())
                requiredTasksForLevel.Add(task);
        }
        if (requiredTasksForLevel.Count == 0)
        {
            log.LogWarning("{ActingPlayer} tried to increase level of {Legion} but no challenge tasks were found", actingPlayer, legion);
            return false;
        }
        foreach (ChallengeTask task in requiredTasksForLevel)
            if (!task.IsCompleted())
                return false;
        return true;
    }
}
