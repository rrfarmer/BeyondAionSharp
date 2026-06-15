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
public class _19026MasterHandicraftersPotential : AbstractQuestHandler
{
    public _19026MasterHandicraftersPotential() : base(19026)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203792).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203792).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798013).AddOnTalkEvent(questId);
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
            if (targetId == 203792)
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
                case 798013:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1011);
                        case DialogAction.SETPRO10:
                            if (!GiveQuestItem(env, 152202049, 1))
                                return true;
                            if (!GiveQuestItem(env, 152020249, 1))
                                return true;
                            qs.SetQuestVarById(0, 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        case DialogAction.SETPRO20:
                            if (!GiveQuestItem(env, 152202050, 1))
                                return true;
                            if (!GiveQuestItem(env, 152020249, 1))
                                return true;
                            qs.SetQuestVarById(0, 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                    }
                    return false;
                case 203792:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            long itemCount1 = player.GetInventory().GetItemCountByItemId(182206767);
                            if (itemCount1 > 0)
                            {
                                RemoveQuestItem(env, 182206767, 1);
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
            if (targetId == 203792)
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
