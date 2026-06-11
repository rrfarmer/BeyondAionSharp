using System.Xml.Serialization;
 using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/MPHealEffect : HealOverTimeEffect. MP heal-over-time; startEffect/onPeriodicAction delegate to base with HealType.MP. Effect/lifeStats red-tolerated.</summary>
[XmlType("MPHealEffect")]
public class MPHealEffect : HealOverTimeEffect
{
    public override void StartEffect(Effect effect)
    {
        base.StartEffect(effect, HealType.MP);
    }

    public override void OnPeriodicAction(Effect effect)
    {
        base.OnPeriodicAction(effect, HealType.MP);
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
