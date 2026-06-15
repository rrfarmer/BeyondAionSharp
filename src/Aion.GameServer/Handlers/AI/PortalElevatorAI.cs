using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/portals/PortalElevatorAI (@author xTz).</summary>
[AIName("portal_elevator")]
public class PortalElevatorAI : PortalAI
{
    public PortalElevatorAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(GetOwner(), EmotionType.EMOTE, 144, 0), true);
        base.HandleUseItemFinish(player);
    }
}
