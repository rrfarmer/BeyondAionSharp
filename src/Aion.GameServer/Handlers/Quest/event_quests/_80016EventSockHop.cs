using System.Collections.Generic;
using Aion.GameServer.Commons.Utils;
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
public class _80016EventSockHop : AbstractQuestHandler
{
    public _80016EventSockHop() : base(80016)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799763).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799763).AddOnTalkEvent(questId);
        qe.RegisterOnBonusApply(questId, BonusType.MOVIE);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.IsStartable())
        {
            if (env.GetTargetId() == 799763)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.QUEST_ACCEPT_1:
                        QuestService.StartEventQuest(env, QuestStatus.START);
                        return SendQuestDialog(env, 1003);
                    default:
                        return SendQuestStartDialog(env);
                }
            }
            return false;
        }

        int var = qs.GetQuestVarById(0);

        if (qs.GetStatus() == QuestStatus.START)
        {
            if (env.GetTargetId() == 799763)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.USE_OBJECT:
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 2375);
                        return false;
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        return CheckQuestItems(env, 0, 1, true, 5, 2716);
                }
            }
        }

        return SendQuestRewardDialog(env, 799763, 0);
    }

    public override HandlerResult OnBonusApplyEvent(QuestEnv env, BonusType bonusType, List<QuestItems> rewardItems)
    {
        if (bonusType != BonusType.MOVIE || env.GetQuestId() != questId)
            return HandlerResult.UNKNOWN;

        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.REWARD)
        {
            if (qs.GetCompleteCount() == 9)
            { // [Event] Hat Box
                rewardItems.Add(new QuestItems(188051106, 1));
            }
            // randomize movie
            PlayQuestMovie(env, Rnd.NextBoolean() ? 103 : 104);
            return HandlerResult.SUCCESS;
        }
        return HandlerResult.FAILED;
    }
}
