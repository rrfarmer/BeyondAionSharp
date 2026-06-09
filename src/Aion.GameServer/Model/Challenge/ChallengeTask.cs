using System;
using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Challenge;

namespace Aion.GameServer.Model.Challenge;

/// <summary>Java parity: model/challenge/ChallengeTask (ViAl). java.sql.Timestamp→DateTimeOffset (getTime()/1000→ToUnixTimeMilliseconds()/1000; new Timestamp(currentTimeMillis)→FromUnixTimeMilliseconds(UtcNow…)); synchronized→lock(this); Map→Dictionary. DataManager.CHALLENGE_DATA/ChallengeTaskTemplate/ChallengeQuestTemplate red-tolerated.</summary>
public class ChallengeTask
{
    private readonly int taskId;
    private readonly int ownerId;
    private Dictionary<int, ChallengeQuest> quests;
    private DateTimeOffset? completeTime;
    private ChallengeTaskTemplate template;

    /// <summary>Used for loading tasks from DAO.</summary>
    public ChallengeTask(int taskId, int ownerId, Dictionary<int, ChallengeQuest> quests, DateTimeOffset? completeTime)
    {
        this.taskId = taskId;
        this.ownerId = ownerId;
        this.quests = quests;
        this.completeTime = completeTime;
        this.template = DataManager.CHALLENGE_DATA.GetTaskByTaskId(taskId);
    }

    /// <summary>Used for creating new tasks in runtime.</summary>
    public ChallengeTask(int ownerId, ChallengeTaskTemplate template)
    {
        this.taskId = template.GetId();
        this.ownerId = ownerId;
        Dictionary<int, ChallengeQuest> quests = new();
        foreach (ChallengeQuestTemplate qt in template.GetQuests())
        {
            ChallengeQuest quest = new(qt, 0);
            quest.SetPersistentState(IPersistable.PersistentState.NEW);
            quests[qt.GetId()] = quest;
        }
        this.quests = quests;
        this.template = template;
    }

    public int GetTaskId()
    {
        return this.taskId;
    }

    public int GetOwnerId()
    {
        return this.ownerId;
    }

    public int GetQuestsCount()
    {
        return quests.Count;
    }

    public Dictionary<int, ChallengeQuest> GetQuests()
    {
        return quests;
    }

    public ChallengeQuest GetQuest(int questId)
    {
        return quests.GetValueOrDefault(questId);
    }

    public DateTimeOffset? GetCompleteTime()
    {
        return completeTime;
    }

    public int GetCompleteTimeEpochSeconds()
    {
        return completeTime == null ? 0 : (int)(completeTime.Value.ToUnixTimeMilliseconds() / 1000);
    }

    public void UpdateCompleteTime()
    {
        lock (this)
        {
            completeTime = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }
    }

    public ChallengeTaskTemplate GetTemplate()
    {
        return this.template;
    }

    public bool IsCompleted()
    {
        bool isCompleted = true;
        foreach (ChallengeQuest quest in quests.Values)
        {
            if (quest.GetCompleteCount() < quest.GetMaxRepeats())
            {
                isCompleted = false;
                break;
            }
        }
        return isCompleted;
    }
}
