using System.Xml.Serialization;
 using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/FPHealEffect : HealOverTimeEffect. FP heal-over-time; startEffect/onPeriodicAction delegate to base with HealType.FP. Effect/lifeStats red-tolerated.</summary>
[XmlType("FPHealEffect")]
public class FPHealEffect : HealOverTimeEffect
{
    public override void StartEffect(Effect effect)
    {
        base.StartEffect(effect, HealType.FP);
    }

    public override void OnPeriodicAction(Effect effect)
    {
        base.OnPeriodicAction(effect, HealType.FP);
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
