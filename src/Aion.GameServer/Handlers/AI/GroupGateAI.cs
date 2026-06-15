using Aion.GameServer.Ai;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/portals/GroupGateAI (@author ATracer, nrg).</summary>
[AIName("groupgate")]
public class GroupGateAI : NpcAI
{
    private const int CANCEL_DIALOG_METERS = 9;

    public GroupGateAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        if (!player.GetCommonData().IsDaeva())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_CAN_NOT_USE_GROUPGATE_BEFORE_CHANGE_CLASS());
            return;
        }

        if (!player.Equals(GetCreator()) && (!player.IsInGroup() || !player.GetPlayerGroup().HasMember(GetCreatorId())))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_CAN_NOT_USE_GROUPGATE_NO_RIGHT());
            return;
        }

        AIActions.AddRequest(this, player, SM_QUESTION_WINDOW.STR_ASK_GROUP_GATE_DO_YOU_ACCEPT_MOVE, CANCEL_DIALOG_METERS, new GroupGateRequest());
    }

    private sealed class GroupGateRequest : AIRequest
    {
        private bool decisionTaken = false;

        public override void AcceptRequest(Creature requester, Player responder, int requestId)
        {
            if (!decisionTaken)
            {
                Npc npc = (Npc)requester;
                switch (npc.GetNpcId())
                {
                    // Group Gates
                    case 833208:
                    case 749017:
                        TeleportService.TeleportTo(responder, 110010000, 1444.9f, 1577.2f, 572.9f, (byte)0, TeleportAnimation.JUMP_IN);
                        break;
                    case 833207:
                    case 749083:
                        TeleportService.TeleportTo(responder, 120010000, 1657.5f, 1398.7f, 194.7f, (byte)0, TeleportAnimation.JUMP_IN);
                        break;
                    // Binding Group Gates
                    case 749131:
                    case 749132:
                        TeleportService.MoveToBindLocation(responder);
                        break;
                }

                decisionTaken = true;
            }
        }

        public override void DenyRequest(Creature requester, Player responder)
        {
            decisionTaken = true;
        }
    }
}
