using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Ritsu
/// </summary>
public class _26940RaidtheLinkgateFoundry : AbstractQuestHandler
{
    public _26940RaidtheLinkgateFoundry() : base(26940)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(802353).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(802353).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(206362).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 802353)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 206362:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                        {
                            if (var == 0)
                                return SendQuestDialog(env, 1352);
                            return false;
                        }
                        case DialogAction.SETPRO1:
                        {
                            return DefaultCloseDialog(env, 0, 1);
                        }
                    }
                    break;
                case 802353:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                        {
                            if (var == 1)
                                return SendQuestDialog(env, 2375);
                            return false;
                        }
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM_SIMPLE:
                        {
                            if (QuestService.CollectItemCheck(env, true))
                            {
                                ChangeQuestStep(env, 1, 1, true);
                                return SendQuestDialog(env, 5);
                            }
                            else
                                return CloseDialogWindow(env);
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 802353)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
