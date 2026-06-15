using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Mr.Poke, Dune11, xTz, Undertrey, vlog
/// </summary>
public class _1146DelicateMandrake : AbstractQuestHandler
{
    public _1146DelicateMandrake() : base(1146)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203123).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203123).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203139).AddOnTalkEvent(questId);
        qe.RegisterOnQuestTimerEnd(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203123)
            { // Gano
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.ASK_QUEST_ACCEPT:
                        return SendQuestDialog(env, 4);
                    case DialogAction.QUEST_ACCEPT_1:
                        if (GiveQuestItem(env, 182200519, 1))
                        {
                            if (QuestService.StartQuest(env))
                            {
                                QuestService.QuestTimerStart(env, 900);
                                return SendQuestDialog(env, 1003);
                            }
                        }
                        return false;
                    case DialogAction.QUEST_REFUSE_1:
                        return SendQuestDialog(env, 1004);
                    case DialogAction.FINISH_DIALOG:
                        return SendQuestSelectionDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 203139)
            { // Krodis
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        if (player.GetInventory().GetItemCountByItemId(182200519) > 0)
                        {
                            return SendQuestDialog(env, 2375);
                        }
                        return false;
                    case DialogAction.SELECT_QUEST_REWARD:
                        RemoveQuestItem(env, 182200519, 1);
                        ChangeQuestStep(env, 0, 0, true);
                        QuestService.QuestTimerEnd(env);
                        return SendQuestDialog(env, 5);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203139)
            { // Krodis
                return SendQuestEndDialog(env);
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
            RemoveQuestItem(env, 182200519, 1);
            QuestService.AbandonQuest(player, questId);
            return true;
        }
        return false;
    }
}
