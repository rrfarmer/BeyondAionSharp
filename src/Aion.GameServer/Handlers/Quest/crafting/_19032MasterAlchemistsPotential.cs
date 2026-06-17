using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Thuatan, Pad
/// </summary>
public class _19032MasterAlchemistsPotential : AbstractQuestHandler
{
    public _19032MasterAlchemistsPotential() : base(19032)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203786).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203786).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203787).AddOnTalkEvent(questId);
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
            if (targetId == 203786)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 203787:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1011);
                        case DialogAction.SETPRO10:
                            if (!GiveQuestItem(env, 152202147, 1))
                                return true;
                            if (!GiveQuestItem(env, 152020248, 1))
                                return true;
                            qs.SetQuestVarById(0, 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        case DialogAction.SETPRO20:
                            if (!GiveQuestItem(env, 152202148, 1))
                                return true;
                            if (!GiveQuestItem(env, 152020248, 1))
                                return true;
                            qs.SetQuestVarById(0, 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                    }
                    return false;
                case 203786:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            long itemCount1 = player.GetInventory().GetItemCountByItemId(182206768);
                            if (itemCount1 > 0)
                            {
                                RemoveQuestItem(env, 182206768, 1);
                                qs.SetStatus(QuestStatus.REWARD);
                                UpdateQuestStatus(env);
                                return SendQuestDialog(env, 1352);
                            }
                            else
                                return SendQuestDialog(env, 2716);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203786)
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
