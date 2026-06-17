using Aion.GameServer.Ai;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Portal;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Findgroup;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/beshmundirTemple/BeshmundirsWalkAI (Gigi, vlog).</summary>
[AIName("beshmundirswalk")]
public class BeshmundirsWalkAI : ActionItemNpcAI
{
    public BeshmundirsWalkAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 10)); // Initial dialog
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        switch (dialogActionId)
        {
            case DialogAction.OPEN_INSTANCE_RECRUIT:
                FindGroupService.GetInstance().ShowInstanceGroups(player, GetOwner());
                break;
            case DialogAction.INSTANCE_ENTRY: // I'm ready to enter
                if (!player.IsInGroup())
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_ENTER_ONLY_PARTY_DON());
                    return true;
                }
                if (player.GetPlayerGroup().IsLeader(player))
                {
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 4762)); // Path selection
                }
                else
                {
                    if (!IsAGroupMemberInInstance(player))
                    {
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_DUNGEON_CANT_ENTER_NOT_OPENED());
                        return true;
                    }
                    MoveToInstance(player, (byte)0);
                }
                break;
            case DialogAction.SELECT_NONE_1: // I'll take the safer path
            case DialogAction.SELECT_NONE_2: // Give me the dangerous path
                AIRequest request = new BeshmundirsWalkRequest(this);
                // 902051 = STR_INSTANCE_DUNGEON_DIFFICULTY_NORMAL (Normal), 902052 = STR_INSTANCE_DUNGEON_DIFFICULTY_HARD (Difficult)
                int pathL10nId = dialogActionId == DialogAction.SELECT_NONE_1 ? 902051 : 902052;
                AIActions.AddRequest(this, player, SM_QUESTION_WINDOW.STR_INSTANCE_DUNGEON_WITH_DIFFICULTY_ENTER_CONFIRM, request, "300170000",
                    ChatUtil.L10n(pathL10nId));
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 4762)); // Path selection
                break;
        }
        return true;
    }

    private bool IsAGroupMemberInInstance(Player player)
    {
        if (player.IsInGroup())
        {
            foreach (Player member in player.GetPlayerGroup().GetMembers())
            {
                if (member.GetWorldId() == 300170000)
                {
                    return true;
                }
            }
        }
        else
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_ENTER_ONLY_PARTY_DON());
        }
        return false;
    }

    private void MoveToInstance(Player player, byte difficult)
    {
        PortalPath portalPath = DataManager.PORTAL2_DATA.GetPortalUsePath(GetNpcId(), player);
        if (portalPath != null)
        {
            PortalService.Port(portalPath, player, GetOwner(), difficult);
        }
    }

    private sealed class BeshmundirsWalkRequest : AIRequest
    {
        private readonly BeshmundirsWalkAI _ai;

        public BeshmundirsWalkRequest(BeshmundirsWalkAI ai)
        {
            _ai = ai;
        }

        public override void AcceptRequest(Creature requester, Player responder, int requestId)
        {
            if (requestId == SM_QUESTION_WINDOW.STR_INSTANCE_DUNGEON_WITH_DIFFICULTY_ENTER_CONFIRM)
            {
                _ai.MoveToInstance(responder, (byte)2);
            }
            else
            {
                _ai.MoveToInstance(responder, (byte)1);
            }
        }
    }
}
