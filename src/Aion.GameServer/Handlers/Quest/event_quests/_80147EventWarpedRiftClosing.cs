using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Model.Templates.Rewards;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Artur
/// </summary>
public class _80147EventWarpedRiftClosing : AbstractQuestHandler
{
    public _80147EventWarpedRiftClosing() : base(80147)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(830246).AddOnTalkEvent(questId);
        qe.RegisterOnBonusApply(questId, BonusType.RIFT);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();

        if (env.GetTargetId() == 0)
            return SendQuestStartDialog(env);

        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);

        if (qs.GetStatus() == QuestStatus.START)
        {
            if (env.GetTargetId() == 830246)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 2375);
                        return false;
                    case DialogAction.SELECT_QUEST_REWARD:
                        RemoveQuestItem(env, 182215138, 1);
                        return DefaultCloseDialog(env, 0, 1, true, true);
                }
            }
        }
        return SendQuestRewardDialog(env, 830246, 0);
    }

    public override HandlerResult OnBonusApplyEvent(QuestEnv env, BonusType bonusType, List<QuestItems> rewardItems)
    {
        if (bonusType != BonusType.RIFT || env.GetQuestId() != questId)
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
