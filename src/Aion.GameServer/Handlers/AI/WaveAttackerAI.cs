using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/drakenspire/WaveAttackerAI (@author Estrayl).</summary>
[AIName("wave_attacker")]
public class WaveAttackerAI : AggressiveNoLootNpcAI
{
    public WaveAttackerAI(Npc owner)
        : base(owner)
    {
    }

    public override void HandleCreatureDetected(Creature creature)
    {
        base.HandleCreatureDetected(creature);
        if (creature.GetTribe().Equals(TribeClass.IDSEAL_PCGUARD))
        {
            foreach (Npc npc in GetOwner().GetPosition().GetWorldMapInstance().GetNpcs(236248))
                GetOwner().GetAggroList().AddHate(npc, 10000);

            foreach (Npc npc in GetOwner().GetPosition().GetWorldMapInstance().GetNpcs(236249))
                GetOwner().GetAggroList().AddHate(npc, 10000);
        }
    }
}
