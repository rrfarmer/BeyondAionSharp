using System.Xml.Serialization;
 using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/FPHealInstantEffect : AbstractHealEffect. FP instant heal; calculate/applyEffect delegate to base with HealType.FP. Effect/lifeStats red-tolerated.</summary>
[XmlType("FPHealInstantEffect")]
public class FPHealInstantEffect : AbstractHealEffect
{
    public override void Calculate(Effect effect)
    {
        base.Calculate(effect, HealType.FP);
    }

    public override void ApplyEffect(Effect effect)
    {
        base.ApplyEffect(effect, HealType.FP);
    }

    public override int GetCurrentStatValue(Effect effect)
    {
        return effect.GetEffected().GetLifeStats().GetCurrentFp();
    }

    public override int GetMaxStatValue(Effect effect)
    {
        return effect.GetEffected().GetLifeStats().GetMaxFp();
    }
}
