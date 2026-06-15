using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/AgrintAI (xTz, Neon).</summary>
[AIName("agrint")]
public class AgrintAI : OneDmgAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(50);

    public AgrintAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        switch (GetNpcId())
        {
            case 218862:
            case 218850:
                PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_HF_SpringAgrintAppear());
                break;
            case 218863:
            case 218851:
                PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_HF_SummerAgrintAppear());
                break;
            case 218864:
            case 218852:
                PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_HF_FallAgrintAppear());
                break;
            case 218865:
            case 218853:
                PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_HF_WinterAgrintAppear());
                break;
        }
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (GetNpcId())
        {
            case 218850:
            case 218851:
            case 218852:
            case 218853:
            {
                int npcId = GetNpcId() + 320;
                RndSpawnInRange(npcId, 1, 2);
                RndSpawnInRange(npcId, 1, 2);
                RndSpawnInRange(npcId, 1, 2);
                RndSpawnInRange(npcId, 1, 2);
                RndSpawnInRange(npcId, 1, 2);
                break;
            }
            case 218862:
            case 218863:
            case 218864:
            case 218865:
            {
                int npcId = GetNpcId() + 308;
                RndSpawnInRange(npcId, 1, 2);
                RndSpawnInRange(npcId, 1, 2);
                RndSpawnInRange(npcId, 1, 2);
                RndSpawnInRange(npcId, 1, 2);
                RndSpawnInRange(npcId, 1, 2);
                break;
            }
        }
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        hpPhases.Reset();
    }

    private void SpawnChests(int npcId)
    {
        RndSpawnInRange(npcId, 1, 6);
        RndSpawnInRange(npcId, 1, 6);
        RndSpawnInRange(npcId, 1, 6);
        RndSpawnInRange(npcId, 1, 6);
        RndSpawnInRange(npcId, 1, 6);
        RndSpawnInRange(npcId, 1, 6);
    }

    protected override void HandleDied()
    {
        switch (GetNpcId())
        {
            case 218850:
                SpawnChests(218874);
                break;
            case 218851:
                SpawnChests(218876);
                break;
            case 218852:
                SpawnChests(218878);
                break;
            case 218853:
                SpawnChests(218880);
                break;
            case 218862:
                SpawnChests(218882);
                break;
            case 218863:
                SpawnChests(218884);
                break;
            case 218864:
                SpawnChests(218886);
                break;
            case 218865:
                SpawnChests(218888);
                break;
        }
        base.HandleDied();
    }
}
