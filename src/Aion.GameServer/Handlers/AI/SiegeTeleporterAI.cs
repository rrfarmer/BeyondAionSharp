using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Siege;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/siege/SiegeTeleporterAI (Source).</summary>
[AIName("siege_teleporter")]
public class SiegeTeleporterAI : GeneralNpcAI
{
    public SiegeTeleporterAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDespawned()
    {
        CanTeleport(false);
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        CanTeleport(false);
        base.HandleDied();
    }

    protected override void HandleSpawned()
    {
        CanTeleport(true);
        base.HandleSpawned();
    }

    private void CanTeleport(bool status)
    {
        int id = ((SiegeNpc)GetOwner()).GetSiegeId();

        SiegeService.GetInstance().GetSiegeLocation(id).SetCanTeleport(status);

        GetPosition().GetWorldMapInstance().ForEachPlayer(player =>
        {
            PacketSendUtility.SendPacket(player, new SM_FORTRESS_INFO(id, status));
        });
    }
}
