using System.Xml.Serialization;
 using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/HealInstantEffect : AbstractHealEffect. HP instant heal; calculate/applyEffect delegate to base with HealType.HP. Effect/lifeStats red-tolerated.</summary>
[XmlType("HealInstantEffect")]
public class HealInstantEffect : AbstractHealEffect
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
}
