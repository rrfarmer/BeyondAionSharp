using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Teleport;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author MrPoke, Nephis, Gigi, vlog
/// </summary>
public class _2422LiquorThatMakesYouVanish : AbstractQuestHandler
{
    public _2422LiquorThatMakesYouVanish() : base(2422)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204326).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204326).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204327).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204375).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204326)
            { // Hapenill
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
            switch (targetId)
            {
                case 204327: // Sveinn
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 1011);
                            }
                            return false;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1); // 1
                    }
                    break;
                case 204375: // Otis
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                            {
                                return SendQuestDialog(env, 1352);
                            }
                            return false;
                        case DialogAction.SETPRO2:
                            qs.SetQuestVar(2);
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            TeleportService.TeleportTo(player, 210020000, player.GetInstanceId(), 535.46f, 2555.62f, 326.63605f, (byte)112);
                            return true;
                        case DialogAction.FINISH_DIALOG:
                            return SendQuestSelectionDialog(env);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204326)
            { // Hapenill
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
