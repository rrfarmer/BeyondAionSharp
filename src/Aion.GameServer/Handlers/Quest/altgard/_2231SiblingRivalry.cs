using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Akiro, Majka
/// </summary>
public class _2231SiblingRivalry : AbstractQuestHandler
{
    public _2231SiblingRivalry() : base(2231)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203620).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203620).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203609).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203612).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203610).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
        {
            targetId = npc.GetNpcId();
        }
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (targetId == 203620) // Lamir
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (targetId == 203609) // Karl
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 0)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1352);
                }
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                {
                    return DefaultCloseDialog(env, 0, 1); // 1
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (targetId == 203612) // Gunmarson
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 1)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1693);
                }
                else if (env.GetDialogActionId() == DialogAction.SETPRO2)
                {
                    return DefaultCloseDialog(env, 1, 2); // 2
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (targetId == 203610) // Kaibech
        {
            if (qs != null)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT && qs.GetStatus() == QuestStatus.START)
                {
                    return SendQuestDialog(env, 2375);
                }
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD && qs.GetStatus() != QuestStatus.COMPLETE)
                {
                    qs.SetQuestVar(2);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return SendQuestEndDialog(env);
                }
                else
                {
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }
}
