using System.Collections.Generic;
using Aion.GameServer.QuestEngine;

namespace Aion.GameServer.Model.Templates.Quest;

/// <summary>Java parity: model/templates/quest/QuestNpc (MrPoke).</summary>
public class QuestNpc
{
    private readonly ISet<int> onQuestStart;
    private readonly List<int> onKillEvent;
    private readonly List<int> onTalkEvent;
    private readonly List<int> onAttackEvent;
    private readonly List<int> onAddAggroListEvent;
    private readonly List<int> onAtDistanceEvent;
    private readonly int npcId;
    private readonly int questRange;

    public QuestNpc(int npcId, int questRange)
    {
        this.npcId = npcId;
        this.questRange = questRange;
        onQuestStart = new HashSet<int>();
        onKillEvent = new List<int>();
        onTalkEvent = new List<int>();
        onAttackEvent = new List<int>();
        onAddAggroListEvent = new List<int>();
        onAtDistanceEvent = new List<int>();
    }

    public QuestNpc(int npcId)
        : this(npcId, 20)
    {
    }

    public void AddOnQuestStart(int questId)
    {
        if (!onQuestStart.Contains(questId))
        {
            onQuestStart.Add(questId);
        }
    }

    public ISet<int> GetOnQuestStart()
    {
        return onQuestStart;
    }

    public void AddOnAttackEvent(int questId)
    {
        if (!onAttackEvent.Contains(questId))
        {
            onAttackEvent.Add(questId);
        }
    }

    public List<int> GetOnAttackEvent()
    {
        return onAttackEvent;
    }

    public void AddOnKillEvent(int questId)
    {
        if (!onKillEvent.Contains(questId))
        {
            onKillEvent.Add(questId);
            Aion.GameServer.QuestEngine.QuestEngine.GetInstance().RegisterCanAct(questId, npcId);
        }
    }

    public List<int> GetOnKillEvent()
    {
        return onKillEvent;
    }

    public void AddOnTalkEvent(int questId)
    {
        if (!onTalkEvent.Contains(questId))
        {
            onTalkEvent.Add(questId);
            Aion.GameServer.QuestEngine.QuestEngine.GetInstance().RegisterCanAct(questId, npcId);
        }
    }

    public List<int> GetOnTalkEvent()
    {
        return onTalkEvent;
    }

    public void AddOnAddAggroListEvent(int questId)
    {
        if (!onAddAggroListEvent.Contains(questId))
        {
            onAddAggroListEvent.Add(questId);
            Aion.GameServer.QuestEngine.QuestEngine.GetInstance().RegisterCanAct(questId, npcId);
        }
    }

    public List<int> GetOnAddAggroListEvent()
    {
        return onAddAggroListEvent;
    }

    public void AddOnAtDistanceEvent(int questId)
    {
        if (!onAtDistanceEvent.Contains(questId))
        {
            onAtDistanceEvent.Add(questId);
            Aion.GameServer.QuestEngine.QuestEngine.GetInstance().RegisterCanAct(questId, npcId);
        }
    }

    public List<int> GetOnDistanceEvent()
    {
        return onAtDistanceEvent;
    }

    public int GetNpcId()
    {
        return npcId;
    }

    public int GetQuestRange()
    {
        return questRange;
    }
}
