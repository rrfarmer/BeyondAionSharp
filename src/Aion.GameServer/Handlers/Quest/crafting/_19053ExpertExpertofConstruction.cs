using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>Java parity: quest/crafting/_19053ExpertExpertofConstruction (Ritsu, Pad).</summary>
public class _19053ExpertExpertofConstruction : AbstractQuestHandler
{
    public _19053ExpertExpertofConstruction() : base(19053)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798450).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798450).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (dialogActionId == DialogAction.QUEST_SELECT && !Aion.GameServer.Services.Craft.CraftSkillUpdateService.GetInstance().CanLearnMoreExpertCraftingSkill(player))
        {
            return SendQuestSelectionDialog(env);
        }

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798450)
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
                case 798450:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                        {
                            return SendQuestDialog(env, 2375);
                        }
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        {
                            if (QuestService.CollectItemCheck(env, true))
                            {
                                ChangeQuestStep(env, 0, 0, true);
                                return SendQuestDialog(env, 5);
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
            if (targetId == 798450)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
