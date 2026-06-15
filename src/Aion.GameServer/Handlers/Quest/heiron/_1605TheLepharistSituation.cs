using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author MrPoke, Nephis
/// </summary>
public class _1605TheLepharistSituation : AbstractQuestHandler
{
    public _1605TheLepharistSituation() : base(1605)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204576).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204576).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204530).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204501).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204577).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (targetId == 204576)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 204530)
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 0)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1352);
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                {
                    ChangeQuestStep(env, 0, 1);
                    return CloseDialogWindow(env);
                }
            }
        }
        else if (targetId == 204501)
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 1)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1693);
                else if (env.GetDialogActionId() == DialogAction.SETPRO2)
                {
                    ChangeQuestStep(env, 1, 2);
                    return CloseDialogWindow(env);
                }
            }
        }
        else if (targetId == 204577)
        {
            if (qs != null)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT && qs.GetStatus() == QuestStatus.START)
                    return SendQuestDialog(env, 2375);
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD && qs.GetStatus() != QuestStatus.COMPLETE)
                    ChangeQuestStep(env, 2, 3, true);
                return SendQuestEndDialog(env);
            }
        }
        return base.OnDialogEvent(env);
    }
}
