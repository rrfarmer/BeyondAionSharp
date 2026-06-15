using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/rakes/SuspiciousCannonAI (xTz).</summary>
[AIName("suspiciouscannon")]
public class SuspiciousCannonAI : ActionItemNpcAI
{
    public SuspiciousCannonAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        int teleportId = GetOwner().GetNpcId() == 730769 ? 247001 : 73001;
        player.SetState(CreatureState.FLYING);
        player.UnsetState(CreatureState.ACTIVE);
        player.SetFlightTeleportId(teleportId);
        PacketSendUtility.SendPacket(player, new SM_EMOTION(player, EmotionType.START_FLYTELEPORT, teleportId, 0));
    }
}
