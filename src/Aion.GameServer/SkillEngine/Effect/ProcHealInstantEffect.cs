using System.Xml.Serialization;
 using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/ProcHealInstantEffect : AbstractHealEffect. HP instant heal; calculate/applyEffect delegate to base with HealType.HP. Effect/lifeStats red-tolerated.</summary>
[XmlType("ProcHealInstantEffect")]
public class ProcHealInstantEffect : AbstractHealEffect
{
    public override void Calculate(Effect effect)
    {
        base.Calculate(effect, HealType.HP);
    }

    public override void ApplyEffect(Effect effect)
    {
        base.ApplyEffect(effect, HealType.HP);
    }

    public override int GetCurrentStatValue(Effect effect)
    {
        return effect.GetEffected().GetLifeStats().GetCurrentHp();
    }

    public override int GetMaxStatValue(Effect effect)
    {
        return effect.GetEffected().GetGameStats().GetMaxHp().GetCurrent();
    }
    public override bool AllowHpHealBoost(Effect effect)
    {
        return false;
    }
}
