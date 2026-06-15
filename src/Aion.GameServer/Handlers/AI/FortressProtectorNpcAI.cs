using Aion.GameServer.Ai;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/siege/FortressProtectorNpcAI (ATracer, Source).</summary>
[AIName("fortress_protector")]
public class FortressProtectorNpcAI : AbstractSiegeProtectorAI
{
    public FortressProtectorNpcAI(Npc owner)
        : base(owner)
    {
    }

    public override float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        if (effected is Npc)
            return damage * 5;
        return damage;
    }

    public override void ModifyOwnerStat(Stat2 stat)
    {
        if (stat.GetStat() == StatEnum.MAXHP && GetOwner().GetRating() == NpcRating.LEGENDARY)
            stat.SetBaseRate(SiegeConfig.FORTRESS_PROTECTOR_HEALTH_MULTIPLIER);
    }
}
