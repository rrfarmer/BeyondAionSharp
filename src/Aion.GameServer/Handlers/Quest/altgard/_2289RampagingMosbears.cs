using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author vlog
/// </summary>
public class _2289RampagingMosbears : AbstractQuestHandler
{
    public _2289RampagingMosbears() : base(2289)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203616).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203616).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203618).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(210564).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(210584).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(210442).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203616) // Gefion
            {
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
                case 203616: // Gefion
                    switch (dialogActionId)
                    {
                        case DialogAction.SETPRO1:
                            return SendQuestSelectionDialog(env);
                        case DialogAction.QUEST_SELECT:
                            if (var == 5)
                            {
                                return SendQuestDialog(env, 1352);
                            }
                            else if (var == 7)
                            {
                                return SendQuestDialog(env, 2034);
                            }
                            return false;
                        case DialogAction.SELECT2_1_1:
                            PlayQuestMovie(env, 62);
                            return SendQuestDialog(env, 1354);
                        case DialogAction.SETPRO2:
                            return DefaultCloseDialog(env, 5, 6); // 6
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            return CheckQuestItems(env, 7, 7, true, 5, 2120); // reward
                        case DialogAction.FINISH_DIALOG:
                            return DefaultCloseDialog(env, 7, 7);
                    }
                    break;
                case 203618: // Skanin
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 6)
                            {
                                return SendQuestDialog(env, 1693);
                            }
                            return false;
                        case DialogAction.SETPRO3:
                            return DefaultCloseDialog(env, 6, 7, 182203017, 1, 0, 0); // 7
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203616) // Gefion
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        int[] mobs = { 210564, 210584 };
        return DefaultOnKillEvent(env, mobs, 0, 5); // 1, 2, 3, 4, 5
    }
}
