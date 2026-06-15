using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Craft;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>Java parity: quest/crafting/_2979ExpertCooksFinalExam (Gigi, Pad).</summary>
public class _2979ExpertCooksFinalExam : AbstractQuestHandler
{
    public _2979ExpertCooksFinalExam() : base(2979)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204100).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204100).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (dialogActionId == DialogAction.QUEST_SELECT && !CraftSkillUpdateService.GetInstance().CanLearnMoreExpertCraftingSkill(player))
        {
            return SendQuestSelectionDialog(env);
        }

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204100)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 204100:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                        {
                            long itemCount1 = player.GetInventory().GetItemCountByItemId(182207955);
                            long itemCount2 = player.GetInventory().GetItemCountByItemId(182207956);
                            long itemCount3 = player.GetInventory().GetItemCountByItemId(182207957);
                            long itemCount4 = player.GetInventory().GetItemCountByItemId(182207958);
                            if (itemCount1 > 0 && itemCount2 > 0 && itemCount3 > 0 && itemCount4 > 0)
                            {
                                RemoveQuestItem(env, 182207955, 1);
                                RemoveQuestItem(env, 182207956, 1);
                                RemoveQuestItem(env, 182207957, 1);
                                RemoveQuestItem(env, 182207958, 1);
                                qs.SetStatus(QuestStatus.REWARD);
                                UpdateQuestStatus(env);
                                return SendQuestDialog(env, 2375);
                            }
                            else
                                return SendQuestDialog(env, 2716);
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204100)
            {
                if (dialogActionId == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                    return SendQuestDialog(env, 5);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
