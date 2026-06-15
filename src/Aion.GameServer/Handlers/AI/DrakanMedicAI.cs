using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npc;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/classNpc/DrakanMedicAI (Cheatkiller).</summary>
[AIName("drakanmedic")]
public class DrakanMedicAI : DrakanPriestAI
{
    public DrakanMedicAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (Rnd.Chance() < 3)
        {
            SpawnServants(GetOwner().GetObjectTemplate().GetRating() == NpcRating.NORMAL ? 281621 : 281839, 1);
        }
    }
}
