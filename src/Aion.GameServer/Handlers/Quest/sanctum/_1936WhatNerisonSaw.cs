using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _1936WhatNerisonSaw : AbstractQuestHandler
{
    public _1936WhatNerisonSaw() : base(1936)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203833).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203833).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204573).AddOnTalkEvent(questId);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 1935);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (targetId == 203833)
        {
            if (qs == null)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 204573)
        {
            if (qs != null)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT && qs.GetStatus() == QuestStatus.START)
                    return SendQuestDialog(env, 2375);
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD && qs.GetStatus() != QuestStatus.COMPLETE)
                {
                    qs.SetQuestVar(1);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return SendQuestEndDialog(env);
                }
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
