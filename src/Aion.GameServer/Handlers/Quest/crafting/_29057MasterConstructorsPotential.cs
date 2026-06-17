using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Ritsu, Pad
/// </summary>
public class _29057MasterConstructorsPotential : AbstractQuestHandler
{
    private static readonly int[] recipesItemIds = { 152208541, 152208542 };
    private static readonly int[] recipesIds = { 155008541, 155008542 };

    public _29057MasterConstructorsPotential() : base(29057)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798452).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798452).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798453).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (dialogActionId == DialogAction.QUEST_SELECT && !Aion.GameServer.Services.Craft.CraftSkillUpdateService.GetInstance().CanLearnMoreMasterCraftingSkill(player))
        {
            return SendQuestSelectionDialog(env);
        }

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798452)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 798453:
                    long kinah = player.GetInventory().GetKinah();
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            switch (var)
                            {
                                case 0:
                                    return SendQuestDialog(env, 1011);
                                case 2:
                                    return SendQuestDialog(env, 4080);
                            }
                            return false;
                        case DialogAction.SETPRO10:
                            if (kinah >= 167500) // Need check how many kinah decrased
                            {
                                if (!GiveQuestItem(env, 152208541, 1))
                                    return true;
                                player.GetInventory().DecreaseKinah(167500);
                                qs.SetQuestVarById(0, 1);
                                UpdateQuestStatus(env);
                                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                                return true;
                            }
                            else
                                return SendQuestDialog(env, 4400);
                        case DialogAction.SETPRO20:
                            if (kinah >= 223000)
                            {
                                if (!GiveQuestItem(env, 152208542, 1))
                                    return true;
                                player.GetInventory().DecreaseKinah(223000);
                                qs.SetQuestVarById(0, 1);
                                UpdateQuestStatus(env);
                                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                                return true;
                            }
                            else
                                return SendQuestDialog(env, 4400);
                    }
                    return false;
                case 798452:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1352);
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            if (QuestService.CollectItemCheck(env, true))
                            {
                                qs.SetStatus(QuestStatus.REWARD);
                                UpdateQuestStatus(env);
                                return SendQuestDialog(env, 5);
                            }
                            else
                            {
                                int checkFailId = 3398;
                                if (player.GetRecipeList().IsRecipePresent(recipesIds[0]) || player.GetRecipeList().IsRecipePresent(recipesIds[1]))
                                    checkFailId = 2716;
                                else if (player.GetInventory().GetItemCountByItemId(recipesItemIds[0]) > 0
                                    || player.GetInventory().GetItemCountByItemId(recipesItemIds[1]) > 0)
                                    checkFailId = 3057;

                                if (checkFailId == 3398)
                                {
                                    qs.SetQuestVar(2);
                                    UpdateQuestStatus(env);
                                }
                                return SendQuestDialog(env, checkFailId);
                            }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798452)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
