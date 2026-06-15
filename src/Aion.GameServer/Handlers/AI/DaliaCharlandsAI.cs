using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/esoterrace/DaliaCharlandsAI (xTz).</summary>
[AIName("dalia_charlands")]
public class DaliaCharlandsAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(75, 50, 25);

    public DaliaCharlandsAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (hpPhases.GetCurrentPhase() > 0 && GetLifeStats().GetHpPercentage() > 80)
            hpPhases.Reset();
        else
            hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case 75:
            case 50:
            case 25:
                SpawnHelpers();
                break;
        }
    }

    private void SpawnHelpers()
    {
        StartWalk((Npc)Spawn(282177, 1173.68f, 674.11f, 297.5f, (sbyte)0), "3002500001");
        StartWalk((Npc)Spawn(282176, 1174.44f, 669.64f, 297.5f, (sbyte)0), "3002500002");
        StartWalk((Npc)Spawn(282178, 1176.2f, 677.32f, 297.5f, (sbyte)0), "3002500003");
    }

    private void StartWalk(Npc npc, string walkId)
    {
        npc.GetSpawn().SetWalkerId(walkId);
        WalkManager.StartWalking((NpcAI)npc.GetAi());
        npc.SetState(CreatureState.ACTIVE, true);
        PacketSendUtility.BroadcastPacket(npc, new SM_EMOTION(npc, EmotionType.CHANGE_SPEED, 0, npc.GetObjectId()));
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        hpPhases.Reset();
    }
}
