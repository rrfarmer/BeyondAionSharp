using System;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.QuestEngine.Model;

/// <summary>Java parity: questEngine/model/QuestState (MrPoke, vlog, Rolandas).</summary>
public class QuestState : IPersistable
{
    private int questId;
    private QuestVars questVars;
    private int questFlags;
    private QuestStatus status;
    private int completeCount;
    private DateTime? completeTime;
    private DateTime? nextRepeatTime;
    private int? reward;
    private IPersistable.PersistentState persistentState;

    public QuestState(int questId, QuestStatus status, int questVars, int flags, int completeCount, DateTime? nextRepeatTime, int? reward,
        DateTime? completeTime)
    {
        this.questId = questId;
        this.status = status;
        this.questVars = new QuestVars(questVars);
        this.questFlags = flags;
        this.completeCount = completeCount;
        this.nextRepeatTime = nextRepeatTime;
        this.reward = reward;
        this.completeTime = completeTime;
        this.persistentState = IPersistable.PersistentState.NEW;
    }

    public QuestState(int questId, QuestStatus status)
        : this(questId, status, 0, 0, status == QuestStatus.COMPLETE ? 1 : 0, null, null,
            status == QuestStatus.COMPLETE ? DateTime.UtcNow : (DateTime?)null)
    {
    }

    public QuestVars GetQuestVars()
    {
        return questVars;
    }

    public void SetQuestVarById(int id, int var)
    {
        questVars.SetVarById(id, var);
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    public int GetQuestVarById(int id)
    {
        return questVars.GetVarById(id);
    }

    public void SetQuestVar(int var)
    {
        questVars.SetVar(var);
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    public QuestStatus GetStatus()
    {
        return status;
    }

    public void SetStatus(QuestStatus status)
    {
        SetStatus(status, true);
    }

    public void SetStatus(QuestStatus status, bool updateCompleteCountAndTime)
    {
        if (status == QuestStatus.COMPLETE && this.status != QuestStatus.COMPLETE && updateCompleteCountAndTime)
        {
            completeTime = DateTime.UtcNow;
            completeCount++;
        }
        this.status = status;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    public DateTime? GetLastCompleteTime()
    {
        return completeTime;
    }

    public int GetQuestId()
    {
        return questId;
    }

    public int GetCompleteCount()
    {
        return completeCount;
    }

    public void SetCompleteCount(int completeCount)
    {
        this.completeCount = completeCount;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    public void SetNextRepeatTime(DateTime? nextRepeatTime)
    {
        this.nextRepeatTime = nextRepeatTime;
    }

    public DateTime? GetNextRepeatTime()
    {
        return nextRepeatTime;
    }

    public void SetRewardGroup(int? reward)
    {
        this.reward = reward;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    /// <returns>The reward group or null if not set.</returns>
    public int? GetRewardGroup()
    {
        return reward;
    }

    /// <returns>True, if the quest is not active or is complete and can currently be repeated.</returns>
    public bool IsStartable()
    {
        return status == QuestStatus.COMPLETE && CanRepeat();
    }

    public bool CanRepeat()
    {
        Aion.GameServer.Model.Templates.QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(questId);
        if (completeCount >= template.GetMaxRepeatCount() && template.GetMaxRepeatCount() != 255)
            return false;
        if (template.IsTimeBased() && nextRepeatTime != null)
        {
            DateTime currentTime = DateTime.UtcNow;
            if (currentTime < nextRepeatTime.Value)
                return false;
        }
        return true;
    }

    public IPersistable.PersistentState GetPersistentState()
    {
        return persistentState;
    }

    public void SetPersistentState(IPersistable.PersistentState persistentState)
    {
        switch (persistentState)
        {
            case IPersistable.PersistentState.DELETED:
                if (this.persistentState == IPersistable.PersistentState.NEW)
                    this.persistentState = IPersistable.PersistentState.NOACTION;
                else
                    this.persistentState = IPersistable.PersistentState.DELETED;
                break;
            case IPersistable.PersistentState.UPDATE_REQUIRED:
                if (this.persistentState == IPersistable.PersistentState.NEW)
                    break;
                goto default;
            default:
                this.persistentState = persistentState;
                break;
        }
    }

    /// <summary>Possibly the second set of quest vars, now named as flags.</summary>
    public int GetFlags()
    {
        return questFlags;
    }

    /// <summary>Possibly the second set of quest vars, now named as flags.</summary>
    public void SetFlags(int questFlags)
    {
        this.questFlags = questFlags;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    public int GetStepGroup()
    {
        return questFlags >> 6;
    }

    public void SetStepGroup(int groupNumber)
    {
        SetFlags(groupNumber << 6);
    }
}
