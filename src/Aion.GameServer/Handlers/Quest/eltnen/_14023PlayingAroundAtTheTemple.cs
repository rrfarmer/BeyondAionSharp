using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Artur, Majka, Pad
/// </summary>
public class _14023PlayingAroundAtTheTemple : AbstractQuestHandler
{
    public _14023PlayingAroundAtTheTemple() : base(14023)
    {
    }

    public override void Register()
    {
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        qe.RegisterQuestNpc(203965).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203967).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();
        if (qs == null)
        {
            return false;
        }

        if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 203965: // Castor
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                                return SendQuestDialog(env, 1011);
                            return false;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1); // 1
                    }
                    break;
                case 203967: // Axelion
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                                return SendQuestDialog(env, 1352);
                            else if (var == 2)
                                return SendQuestDialog(env, 1693);
                            return false;
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            if (var == 2 && QuestService.CollectItemCheck(env, true))
                            {
                                ChangeQuestStep(env, 2, 2, true);
                                return SendQuestDialog(env, 10000);
                            }
                            else
                            {
                                return SendQuestDialog(env, 10001);
                            }
                        case DialogAction.SETPRO2:
                            return DefaultCloseDialog(env, 1, 2); // 2
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203965)
            { // Castor
                if (dialogActionId == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 2034);
                else if (dialogActionId == DialogAction.SET_SUCCEED)
                    return SendQuestDialog(env, 5);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 14020);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 14020);
    }
}
