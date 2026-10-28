using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/RiftProtectorAI (Estrayl).</summary>
[AIName("rift_protector")]
public class RiftProtectorAI : AggressiveNpcAI
{
    public RiftProtectorAI(Npc owner)
        : base(owner)
    {
    }

    public override void ModifyOwnerStat(Stat2 stat)
    {
        // Selected retail profiles already provide the complete base HP.
        // Keep Java's ten-percent tuning only for unselected/owned contexts.
        if (stat.GetStat() == StatEnum.MAXHP && GetOwner().GetGameStats()?.StatisticsProfile is null)
            stat.SetBaseRate(0.1f);
    }
}
