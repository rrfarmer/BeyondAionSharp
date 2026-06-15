using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// This AI handles mobs which are blocking geysers in Inggison.
/// Java parity: ai/worlds/inggison/GeyserBlockerAI (@author Neon).
/// </summary>
[AIName("geyserblocker")]
public class GeyserBlockerAI : AggressiveNpcAI
{
    private int staticId = -1;

    public GeyserBlockerAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        if (staticId == -1) // no geyser was despawned on spawn, so we cannot respawn it
            return;
        SpawnTemplate spawnPoint = GetOwner().GetSpawn();
        Spawn(700545, spawnPoint.GetX(), spawnPoint.GetY(), spawnPoint.GetZ(), (sbyte)0, staticId);
        PacketSendUtility.BroadcastPacket(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_WINDBOX_TRIGGER_ON_INFO(),
            player => PositionUtil.IsInRange(GetOwner(), player, 30));
    }

    protected override void HandleCreatureSee(Creature creature)
    {
        base.HandleCreatureSee(creature);
        if (creature is Npc)
        {
            Npc npc = (Npc)creature;
            if (npc.GetNpcId() == 700545 && PositionUtil.IsInRange(GetOwner(), npc, 5))
            {
                staticId = npc.GetSpawn().GetStaticId();
                npc.GetController().Delete();
            }
        }
    }
}
