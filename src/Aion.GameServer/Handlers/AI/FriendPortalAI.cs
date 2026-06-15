using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/portals/FriendPortalAI (@author Rolandas).</summary>
[AIName("friendportal")]
public class FriendPortalAI : NpcAI
{
    public FriendPortalAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        SummonedHouseNpc me = (SummonedHouseNpc)GetOwner();
        int playerOwner = me.GetCreator().GetOwnerId();

        bool allowed = playerOwner == 0 || player.GetObjectId() == playerOwner || player.GetFriendList().GetFriend(playerOwner) != null
            || (player.GetLegion() != null && player.GetLegion().IsMember(playerOwner));

        if (allowed)
        {
            PacketSendUtility.SendPacket(player, new SM_FRIEND_LIST()); // client needs friendlist info to list entries in housing friendlist
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetOwner().GetObjectId(), DialogPage.HOUSING_FRIENDLIST.Id()));
        }
        else
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_HOUSING_TELEPORT_CANT_USE());
        }
    }

    protected override void HandleDialogFinish(Player player)
    {
    }
}
