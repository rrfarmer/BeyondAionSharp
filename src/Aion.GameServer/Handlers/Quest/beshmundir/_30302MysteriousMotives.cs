using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Gigi
/// </summary>
public class _30302MysteriousMotives : AbstractQuestHandler
{
    public _30302MysteriousMotives() : base(30302)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799225).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799225).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799240).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799243).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (targetId == 799225)
        {
            if (qs == null)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                    return SendQuestStartDialog(env);
            }
            else if (qs.GetStatus() == QuestStatus.START)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2375);
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                {
                    ChangeQuestStep(env, 2, 3, true);
                    return SendQuestEndDialog(env);
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                return SendQuestEndDialog(env);
            }
        }
        else if (targetId == 799240)
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
        else if (targetId == 799243)
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START)
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
        return base.OnDialogEvent(env);
    }
}
