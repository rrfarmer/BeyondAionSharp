using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/drakenspire/WaveAttackerAI (@author Estrayl).</summary>
/// <remarks>
/// <b>Superseded, and bound to nothing on purpose.</b> Java binds thirteen npcs to <c>wave_attacker</c>
/// and this port binds none: the eight attackers now run <see cref="SealWaveAttackerAI"/> and the five
/// leaders <see cref="SealWaveLeaderAI"/>, from Drakenspire's own retail patterns. That is the
/// sanctioned exception in CLAUDE.md — retail AI pattern data outranks aionemu's approximation — and it
/// is logged in docs/retail-ai-fidelity.md.
/// <para>
/// Kept rather than deleted because it is the aionemu reference the replacement was measured against.
/// <b>Do not "restore" the thirteen bindings</b>: an audit that flags this class as unused is reporting
/// the intended state.
/// </para>
/// </remarks>
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
