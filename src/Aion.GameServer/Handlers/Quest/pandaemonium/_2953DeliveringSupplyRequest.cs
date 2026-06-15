using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Altaress, vlog
/// </summary>
public class _2953DeliveringSupplyRequest : AbstractQuestHandler
{
    public _2953DeliveringSupplyRequest() : base(2953)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204191).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204191).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204071).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204191) // Doman
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else
                {
                    return SendQuestStartDialog(env, 182207039, 1);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 204071) // Veldina
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1352);
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    return DefaultCloseDialog(env, 0, 1); // 1
                }
            }
            else if (targetId == 204191) // Doman
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (var == 1)
                    {
                        return SendQuestDialog(env, 2375);
                    }
                }
                else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                {
                    ChangeQuestStep(env, 1, 1, true);
                    return SendQuestDialog(env, 5);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204191) // Doman
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
