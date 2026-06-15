using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _2758CarryTheFlame : AbstractQuestHandler
{
    public _2758CarryTheFlame() : base(2758)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(279000).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(790016).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 279000)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 279000)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                if (env.GetDialogActionId() == DialogAction.SETPRO1)
                {
                    GiveQuestItem(env, 182205645, 1);
                    QuestService.QuestTimerStart(env, 900);
                    return DefaultCloseDialog(env, 0, 1);
                }
            }
            else if (targetId == 790016)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1352);
                }
                else if (env.GetDialogActionId() == DialogAction.SET_SUCCEED)
                {
                    QuestService.QuestTimerEnd(env);
                    RemoveQuestItem(env, 182205645, 1);
                    qs.SetStatus(QuestStatus.REWARD);
                    qs.SetQuestVar(1);
                    UpdateQuestStatus(env);
                    return SendQuestDialog(env, 5);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 790016)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 5);
                }
                else
                {
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }

    public override bool OnQuestTimerEndEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (var > 1)
            {
                RemoveQuestItem(env, 182205645, 1);
                ChangeQuestStep(env, var, 0);
                return true;
            }
        }
        return false;
    }
}
