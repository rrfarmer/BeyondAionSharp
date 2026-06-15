using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author vlog
/// </summary>
public class _2123TheImprisonedGourmet : AbstractQuestHandler
{
    public _2123TheImprisonedGourmet() : base(2123)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203550).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203550).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700128).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203550) // Munin
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    default:
                        return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 203550: // Munin
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1352);
                        case DialogAction.SETPRO1:
                            if (player.GetInventory().GetItemCountByItemId(182203121) >= 1)
                            {
                                qs.SetQuestVar(5); // 5
                                qs.SetRewardGroup(0);
                                qs.SetStatus(QuestStatus.REWARD); // rewatd
                                UpdateQuestStatus(env);
                                RemoveQuestItem(env, 182203121, 1);
                                return SendQuestDialog(env, 5);
                            }
                            else
                            {
                                return SendQuestDialog(env, 1693);
                            }
                        case DialogAction.SETPRO2:
                            if (player.GetInventory().GetItemCountByItemId(182203122) >= 1)
                            {
                                qs.SetQuestVar(6); // 6
                                qs.SetRewardGroup(1);
                                qs.SetStatus(QuestStatus.REWARD); // rewatd
                                UpdateQuestStatus(env);
                                RemoveQuestItem(env, 182203122, 1);
                                return SendQuestDialog(env, 6);
                            }
                            else
                            {
                                return SendQuestDialog(env, 1693);
                            }
                        case DialogAction.SETPRO3:
                            if (player.GetInventory().GetItemCountByItemId(182203123) >= 1)
                            {
                                qs.SetQuestVar(7); // 7
                                qs.SetRewardGroup(2);
                                qs.SetStatus(QuestStatus.REWARD); // rewatd
                                UpdateQuestStatus(env);
                                RemoveQuestItem(env, 182203123, 1);
                                return SendQuestDialog(env, 7);
                            }
                            else
                            {
                                return SendQuestDialog(env, 1693);
                            }
                        case DialogAction.FINISH_DIALOG:
                            return SendQuestSelectionDialog(env);
                    }
                    break;
                case 700128: // Methu Egg
                    return true;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203550) // Munin
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
