using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Questengine.Handlers.Models;
using Aion.GameServer.Questengine.Handlers.Models.XmlQuest.Events;
using Aion.GameServer.Questengine.Model;

namespace Aion.GameServer.Questengine.Handlers.Template;

/// <summary>Java parity: questEngine/handlers/template/XmlQuest (Mr.Poke, Bobobear, Pad). Set.addAll→UnionWith; Set.equals→SetEquals; List.addAll→AddRange; delegates to OnTalkEvent/OnKillEvent model operate(); DataManager/Monster/OnTalkEvent/OnKillEvent red-tolerated.</summary>
public class XmlQuest : AbstractTemplateQuestHandler
{
    private readonly HashSet<int> startNpcIds = new();
    private readonly HashSet<int> endNpcIds = new();
    private readonly List<OnTalkEvent> onTalkEvents = new();
    private readonly List<OnKillEvent> onKillEvents = new();
    private readonly bool isDataDriven;

    public XmlQuest(int questId, List<int> startNpcIds, List<int> endNpcIds, List<OnTalkEvent> onTalkEvents, List<OnKillEvent> onKillEvents) : base(questId)
    {
        if (startNpcIds != null)
            this.startNpcIds.UnionWith(startNpcIds);
        if (endNpcIds != null)
            this.endNpcIds.UnionWith(endNpcIds);
        else
            this.endNpcIds.UnionWith(this.startNpcIds);
        if (onTalkEvents != null)
            this.onTalkEvents.AddRange(onTalkEvents);
        if (onKillEvents != null)
            this.onKillEvents.AddRange(onKillEvents);
        isDataDriven = DataManager.QUEST_DATA.GetQuestById(questId).IsDataDriven();
    }

    public override void Register()
    {
        foreach (int startNpcId in startNpcIds)
        {
            qe.RegisterQuestNpc(startNpcId).AddOnQuestStart(questId);
            qe.RegisterQuestNpc(startNpcId).AddOnTalkEvent(questId);
        }
        if (!endNpcIds.SetEquals(startNpcIds))
        {
            foreach (int endNpcId in endNpcIds)
            {
                qe.RegisterQuestNpc(endNpcId).AddOnTalkEvent(questId);
            }
        }
        foreach (OnTalkEvent onTalkEvent in onTalkEvents)
        {
            foreach (int npcId in onTalkEvent.GetIds())
            {
                qe.RegisterQuestNpc(npcId).AddOnTalkEvent(questId);
            }
        }
        foreach (OnKillEvent onKillEvent in onKillEvents)
        {
            foreach (Monster monster in onKillEvent.GetMonsters())
            {
                foreach (int monsterId in monster.GetNpcIds())
                {
                    qe.RegisterQuestNpc(monsterId).AddOnKillEvent(questId);
                }
            }
        }
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        //env.SetQuestId(questId);
        foreach (OnTalkEvent onTalkEvent in onTalkEvents)
        {
            if (onTalkEvent.Operate(env))
                return true;
        }

        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (startNpcIds.Contains(targetId))
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, isDataDriven ? 4762 : 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD && endNpcIds.Contains(targetId))
        {
            return SendQuestEndDialog(env);
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        //env.SetQuestId(questId);
        foreach (OnKillEvent onKillEvent in onKillEvents)
        {
            if (onKillEvent.Operate(env))
                return true;
        }
        return false;
    }
}
