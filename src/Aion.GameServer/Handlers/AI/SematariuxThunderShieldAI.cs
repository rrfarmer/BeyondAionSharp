using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/inggison/SematariuxThunderShieldAI (Estrayl).</summary>
[AIName("sematariux_thunder_shield")]
public class SematariuxThunderShieldAI : AggressiveNpcAI
{
    public SematariuxThunderShieldAI(Npc owner)
        : base(owner)
    {
    }

    public override void ModifyOwnerStat(Stat2 stat)
    {
        if (stat.GetStat() == StatEnum.MAXHP)
            stat.SetBaseRate(0.1f);
    }

    public override float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        return damage * 0.5f;
    }
}
