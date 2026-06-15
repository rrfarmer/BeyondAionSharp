using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Mr. Poke, vlog, Majka
/// </summary>
public class _2006HitThemWhereitHurts : AbstractQuestHandler
{
    public _2006HitThemWhereitHurts() : base(2006)
    {
    }

    public override void Register()
    {
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        qe.RegisterQuestNpc(203540).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700095).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203516).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;
        int dialogActionId = env.GetDialogActionId();
        int var = qs.GetQuestVarById(0);
        int targetId = env.GetTargetId();

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 203540: // Mijou
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 1011);
                            }
                            else if (var == 1)
                            {
                                return SendQuestDialog(env, 1352);
                            }
                            return false;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1); // 1
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            return CheckQuestItems(env, 1, 1, true, 1438, 1353); // reward
                        case DialogAction.FINISH_DIALOG:
                            return SendQuestSelectionDialog(env);
                    }
                    break;
                case 700095: // Mau Grain Sack
                    if (var == 1)
                    {
                        return true; // give loot
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203516) // Ulgorn
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 1693);
                }
                else
                {
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 2100);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 2100);
    }
}
