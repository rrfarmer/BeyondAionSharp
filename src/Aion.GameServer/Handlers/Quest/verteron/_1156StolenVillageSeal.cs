using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Rhys2002, vlog
/// </summary>
public class _1156StolenVillageSeal : AbstractQuestHandler
{
    public _1156StolenVillageSeal() : base(1156)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203128).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203128).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700003).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798003).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203128)
            { // Santenius
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
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
            switch (targetId)
            {
                case 700003: // Item Stack
                    if (dialogActionId == DialogAction.USE_OBJECT)
                    {
                        if (var == 0)
                        {
                            return SendQuestDialog(env, 1352);
                        }
                    }
                    else if (dialogActionId == DialogAction.SETPRO1)
                    {
                        ChangeQuestStep(env, 0, 1); // 1
                        return CloseDialogWindow(env);
                    }
                    break;
                case 798003: // Gaphyrk
                    if (dialogActionId == DialogAction.QUEST_SELECT)
                    {
                        if (var == 1)
                        {
                            return SendQuestDialog(env, 2375);
                        }
                    }
                    else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                    {
                        ChangeQuestStep(env, 1, 1, true); // reward
                        return SendQuestDialog(env, 5);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798003)
            { // Gaphyrk
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
