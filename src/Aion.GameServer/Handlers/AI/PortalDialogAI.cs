using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Autogroup;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Portal;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Findgroup;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/portals/PortalDialogAI (@author xTz, vlog).</summary>
[AIName("portal_dialog")]
public class PortalDialogAI : PortalAI
{
    public PortalDialogAI(Npc owner)
        : base(owner)
    {
    }

    /// <summary>
    /// Standard value. Can be changed through override
    /// </summary>
    protected int rewardDialogId = 5;

    /// <summary>
    /// Standard value. Can be changed through override
    /// </summary>
    protected int startingDialogId = 10;

    /// <summary>
    /// Standard value. Can be changed through override
    /// </summary>
    protected int questDialogId = 10;

    protected override void HandleDialogStart(Player player)
    {
        if (GetTalkDelayInMs() == 0)
        {
            CheckDialog(player);
        }
        else
        {
            base.HandleDialogStart(player);
        }
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        QuestEnv env = new QuestEnv(GetOwner(), player, questId, dialogActionId);
        env.SetExtendedRewardIndex(extendedRewardIndex);
        if (questId > 0 && QuestEngine.QuestEngine.GetInstance().OnDialog(env))
        {
            return true;
        }

        switch (dialogActionId)
        {
            case DialogAction.INSTANCE_PARTY_MATCH: // auto groups
                AutoGroupType? agt = AutoGroupTypeExtensions.GetAutoGroup(player.GetLevel(), GetNpcId());
                if (agt != null)
                    PacketSendUtility.SendPacket(player, new SM_AUTO_GROUP(agt.Value.GetTemplate().GetMaskId()));
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
                return true;
            case DialogAction.OPEN_INSTANCE_RECRUIT:
                FindGroupService.GetInstance().ShowInstanceGroups(player, GetOwner());
                return true;
            case DialogAction.SELECT1_1:
                if (!player.IsInTeam() && DataManager.AUTO_GROUP.GetRecruitableInstanceMaskIds(GetNpcId()) != null)
                {
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1182)); // show OPEN_INSTANCE_RECRUIT option
                    return true;
                }

                break;
        }

        if (questId == 0)
        {
            PortalPath portalPath = DataManager.PORTAL2_DATA.GetPortalDialogPath(GetNpcId(), dialogActionId, player);
            if (portalPath != null)
                PortalService.Port(portalPath, player, GetOwner());
            return true;
        }

        return false;
    }

    protected override void HandleUseItemFinish(Player player)
    {
        CheckDialog(player);
    }

    protected virtual void CheckDialog(Player player)
    {
        if (!DialogService.IsInteractionAllowed(player, GetOwner()))
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
            return;
        }

        int npcId = GetNpcId();
        int teleportationDialogId = DataManager.PORTAL2_DATA.GetTeleportDialogId(npcId);
        List<int> relatedQuests = QuestEngine.QuestEngine.GetInstance().GetQuestNpc(npcId).GetOnTalkEvent();
        bool playerHasQuest = false;
        bool playerCanStartQuest = false;
        if (relatedQuests.Count > 0)
        {
            foreach (int questId in relatedQuests)
            {
                QuestState qs = player.GetQuestStateList().GetQuestState(questId);
                if (qs != null && (qs.GetStatus() == QuestStatus.START || qs.GetStatus() == QuestStatus.REWARD))
                {
                    playerHasQuest = true;
                    break;
                }
                else if (qs == null || qs.IsStartable())
                {
                    if (QuestService.CheckStartConditions(player, questId, false))
                    {
                        playerCanStartQuest = true;
                    }
                }
            }
        }

        if (playerHasQuest)
        { // show quest selection dialog and handle teleportation in script, if needed
            bool isRewardStep = false;
            foreach (int questId in relatedQuests)
            {
                QuestState qs = player.GetQuestStateList().GetQuestState(questId);
                if (qs != null && qs.GetStatus() == QuestStatus.REWARD)
                { // reward dialog
                    QuestEnv env = new QuestEnv(GetOwner(), player, questId, DialogAction.USE_OBJECT);
                    isRewardStep = QuestEngine.QuestEngine.GetInstance().OnDialog(env);
                    if (isRewardStep)
                        break;
                }
            }

            if (!isRewardStep) // normal dialog
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), questDialogId));
        }
        else if (playerCanStartQuest)
        { // start quest dialog
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), startingDialogId));
        }
        else
        { // show teleportation dialog
            switch (npcId)
            {
                case 831117:
                case 831131:
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1012));
                    break;
                case 730841:
                case 730883:
                case 804621:
                case 804624:
                case 804625:
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 4762));
                    break;
                case 731583:
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 10));
                    break;
                case 731570:
                    if (player.GetRace() == Race.ASMODIANS)
                    {
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1352)); // seized danuar sanctuary
                    }
                    else
                    {
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011)); // danuar sanctuary
                    }

                    break;
                case 731549:
                    if (player.GetRace() == Race.ELYOS)
                    {
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011)); // seized danuar sanctuary
                    }
                    else
                    {
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1352)); // danuar sanctuary
                    }

                    break;
                default:
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), teleportationDialogId));
                    break;
            }
        }
    }
}
