using Aion.GameServer.Ai;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author xTz, Rolandas
/// </summary>
[AIName("housegate")]
public class HouseGateAI : NpcAI
{
    public HouseGateAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        int creatorId = GetCreatorId();
        bool isCreatorOrFriend = player.Equals(GetCreator()) || player.GetFriendList().GetFriend(creatorId) != null
            || player.IsInGroup() && player.GetCurrentGroup().HasMember(creatorId);
        if (!isCreatorOrFriend)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_HOUSING_TELEPORT_CANT_USE());
            return;
        }

        House house = HousingService.GetInstance().FindActiveHouse(creatorId);
        // Uses skill but doesn't have house
        if (house == null)
        {
            if (player.GetObjectId() == creatorId)
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_HOUSING_TELEPORT_NEED_HOUSE());
            else
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_HOUSING_TELEPORT_CANT_USE());
            return;
        }

        if (!player.GetCommonData().IsDaeva())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_ENTER_NO_RIGHT(house.GetAddress().GetId()));
            return;
        }

        if (!house.CanEnter(player))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_ENTER_NO_RIGHT2());
            return;
        }
        bool returnBattle = true;
        foreach (ZoneInstance zone in player.FindZones())
        {
            if (!zone.CanReturnToBattle())
            {
                returnBattle = false;
                break;
            }
        }
        int requestId = SM_QUESTION_WINDOW.STR_ASK_GROUP_GATE_DO_YOU_ACCEPT_MOVE;
        if (!returnBattle)
            requestId = SM_QUESTION_WINDOW.STR_HOUSE_GATE_ACCEPT_MOVE_DONT_RETURN;

        AIActions.AddRequest(this, player, requestId, 9, new HouseGateRequest(house));
    }

    private sealed class HouseGateRequest : AIRequest
    {
        private readonly House house;
        private bool decided = false;

        public HouseGateRequest(House house)
        {
            this.house = house;
        }

        public override void AcceptRequest(Creature requester, Player responder, int requestId)
        {
            if (decided)
                return;

            WorldMapInstance instance = InstanceService.GetOrCreateHouseInstance(house);
            bool canReturnToBattle = true;
            foreach (ZoneInstance zone in responder.FindZones())
            {
                if (!zone.CanReturnToBattle())
                {
                    canReturnToBattle = false;
                    break;
                }
            }
            if (!canReturnToBattle)
            {
                responder.SetBattleReturnCoords(0, null);
            }
            else
            {
                PacketSendUtility.SendPacket(responder, new SM_HOUSE_TELEPORT(house.GetAddress().GetId(), responder.GetObjectId()));
                responder.SetBattleReturnCoords(responder.GetWorldId(), new float[] { responder.GetX(), responder.GetY(), responder.GetZ() });
            }
            TeleportService.TeleportTo(responder, instance, house.GetX(), house.GetY(), house.GetZ(), (byte)house.GetTeleportHeading(),
                TeleportAnimation.JUMP_IN_GATE);
            decided = true;
        }

        public override void DenyRequest(Creature requester, Player responder)
        {
            decided = true;
        }
    }
}
