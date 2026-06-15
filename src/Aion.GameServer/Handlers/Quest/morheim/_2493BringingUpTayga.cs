using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author MrPoke, Nephis, vlog
/// </summary>
public class _2493BringingUpTayga : AbstractQuestHandler
{
    public _2493BringingUpTayga() : base(2493)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204325).AddOnQuestStart(questId);
        qe.RegisterOnLogOut(questId);
        qe.RegisterQuestNpc(204325).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204435).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204325) // Ipoderr
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 204435) // Purra? 1st Spot
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (var == 0)
                    {
                        return SendQuestDialog(env, 1011);
                    }
                }
                else if (dialogActionId == DialogAction.SET_SUCCEED)
                {
                    env.GetVisibleObject().GetController().DeleteAndScheduleRespawn();
                    ChangeQuestStep(env, 0, 0, true); // reward
                    return CloseDialogWindow(env);
                }
            }
            else if (targetId == 204436 || targetId == 204437 || targetId == 204438) // Purra? other Spots
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (var == 0)
                    {
                        return SendQuestDialog(env, 1353);
                    }
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204325) // Ipoderr
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 10002);
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
