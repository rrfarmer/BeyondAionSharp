using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Tibald
/// </summary>
[AIName("occupy_rentus_portal")]
public class OccupiedRentusBasePortalAI : PortalDialogAI
{
    public OccupiedRentusBasePortalAI(Npc owner) : base(owner)
    {
    }

    protected override void CheckDialog(Player player)
    {
        if (GetOwner().GetNpcId() == 832991)
        {
            if (player.GetRace() == Race.ASMODIANS)
            {
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
            }
            else
            {
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 10));
            }
        }
        else if (GetOwner().GetNpcId() == 832992)
        {
            if (player.GetRace() == Race.ASMODIANS)
            {
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 10));
            }
            else
            {
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
            }
        }
        else
        {
            base.CheckDialog(player);
        }
    }
}
