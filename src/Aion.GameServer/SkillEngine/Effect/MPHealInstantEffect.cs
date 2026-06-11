using System.Xml.Serialization;
 using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/MPHealInstantEffect : AbstractHealEffect. MP instant heal; calculate/applyEffect delegate to base with HealType.MP. Effect/lifeStats red-tolerated.</summary>
[XmlType("MPHealInstantEffect")]
public class MPHealInstantEffect : AbstractHealEffect
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
