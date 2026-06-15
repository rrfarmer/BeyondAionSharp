using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/nightmareCircus/SolidIronChainAI (@author Ritsu).</summary>
[AIName("solidironchain")]
public class SolidIronChainAI : AggressiveNpcAI
{
    public SolidIronChainAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        PacketSendUtility.BroadcastToMap(GetOwner(), new SM_PLAY_MOVIE(false, 0, 0, 983, true));
    }
}
