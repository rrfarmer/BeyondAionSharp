using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Craft;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>Java parity: quest/crafting/_2974ExpertArmorsmithsTest (Gigi, Pad).</summary>
public class _2974ExpertArmorsmithsTest : AbstractQuestHandler
{
    public _2974ExpertArmorsmithsTest() : base(2974)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204106).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204106).AddOnTalkEvent(questId);
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
            if (targetId == 204106)
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
                case 204106:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                        {
                            long itemCount1 = player.GetInventory().GetItemCountByItemId(182207965);
                            if (itemCount1 > 0)
                            {
                                RemoveQuestItem(env, 182207965, 1);
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
            if (targetId == 204106)
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
