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
public class _29026MasterHandicraftersPotential : AbstractQuestHandler
{
    public _29026MasterHandicraftersPotential() : base(29026)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204108).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204108).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798060).AddOnTalkEvent(questId);
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
            if (targetId == 204108)
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
                case 798060:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1011);
                        case DialogAction.SETPRO10:
                            if (!GiveQuestItem(env, 152207051, 1))
                                return true;
                            if (!GiveQuestItem(env, 152029250, 1))
                                return true;
                            qs.SetQuestVarById(0, 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        case DialogAction.SETPRO20:
                            if (!GiveQuestItem(env, 152207052, 1))
                                return true;
                            if (!GiveQuestItem(env, 152029250, 1))
                                return true;
                            qs.SetQuestVarById(0, 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                    }
                    return false;
                case 204108:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            long itemCount1 = player.GetInventory().GetItemCountByItemId(182207901);
                            if (itemCount1 > 0)
                            {
                                RemoveQuestItem(env, 182207901, 1);
                                qs.SetStatus(QuestStatus.REWARD);
                                UpdateQuestStatus(env);
                                return SendQuestDialog(env, 1352);
                            }
                            else
                                return SendQuestDialog(env, 10001);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204108)
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
