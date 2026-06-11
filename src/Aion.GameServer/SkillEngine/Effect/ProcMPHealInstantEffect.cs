using System.Xml.Serialization;
 using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/ProcMPHealInstantEffect : AbstractHealEffect. MP instant heal; calculate/applyEffect delegate to base with HealType.MP. Effect/lifeStats red-tolerated.</summary>
[XmlType("ProcMPHealInstantEffect")]
public class ProcMPHealInstantEffect : AbstractHealEffect
{
    public override void Calculate(Effect effect)
    {
        base.Calculate(effect, HealType.MP);
    }

    public override void ApplyEffect(Effect effect)
    {
        base.ApplyEffect(effect, HealType.MP);
    }

    public override int GetCurrentStatValue(Effect effect)
    {
        return effect.GetEffected().GetLifeStats().GetCurrentMp();
    }

    public override int GetMaxStatValue(Effect effect)
    {
        return effect.GetEffected().GetGameStats().GetMaxMp().GetCurrent();
    }
}
