using System.Xml.Serialization;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/HealEffect (kecimis) : HealOverTimeEffect. HP heal-over-time; delegates to base 2-arg StartEffect/OnPeriodicAction with HealType.HP. Effect/lifeStats red-tolerated.</summary>
[XmlType("HealEffect")]
public class HealEffect : HealOverTimeEffect
{
    public override void StartEffect(Effect effect)
    {
        base.StartEffect(effect, HealType.HP);
    }

    public override void OnPeriodicAction(Effect effect)
    {
        base.OnPeriodicAction(effect, HealType.HP);
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
