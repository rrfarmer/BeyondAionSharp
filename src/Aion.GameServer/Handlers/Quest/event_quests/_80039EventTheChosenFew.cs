using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Model.Templates.Rewards;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Rolandas
/// </summary>
public class _80039EventTheChosenFew : AbstractQuestHandler
{
    public _80039EventTheChosenFew() : base(80039)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799780).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799780).AddOnTalkEvent(questId);
        qe.RegisterOnBonusApply(questId, BonusType.LUNAR);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.IsStartable())
            return false;

        if (qs.GetStatus() == QuestStatus.START || qs.GetStatus() == QuestStatus.COMPLETE && QuestService.CollectItemCheck(env, false))
        {
            if (targetId == 799780)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                    return SendQuestDialog(env, 1003);
                else if (env.GetDialogActionId() == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                    return CheckQuestItems(env, 0, 0, true, 5, 2716);
                else if (env.GetDialogActionId() == DialogAction.SELECTED_QUEST_NOREWARD)
                    return SendQuestRewardDialog(env, 799780, 5);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                return SendQuestDialog(env, 5);
            return SendQuestEndDialog(env);
        }
        return SendQuestRewardDialog(env, 799780, 0);
    }

    public override HandlerResult OnBonusApplyEvent(QuestEnv env, BonusType bonusType, List<QuestItems> rewardItems)
    {
        if (bonusType != BonusType.LUNAR || env.GetQuestId() != questId)
            return HandlerResult.UNKNOWN;
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && (qs.GetStatus() == QuestStatus.START || qs.GetStatus() == QuestStatus.COMPLETE))
        {
            if (qs.GetQuestVarById(0) == 0)
                return HandlerResult.SUCCESS;
        }
        return HandlerResult.FAILED;
    }
}
