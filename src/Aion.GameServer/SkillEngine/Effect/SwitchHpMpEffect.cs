using System.Xml.Serialization;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/SwitchHpMpEffect (ATracer) : EffectTemplate. **CreatureLifeStats&lt;? extends Creature&gt;→non-generic CreatureLifeStats**; swaps current HP/MP. EffectTemplate/Effect/CreatureLifeStats red-tolerated.</summary>
[XmlType("SwitchHpMpEffect")]
public class SwitchHpMpEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        CreatureLifeStats lifeStats = effect.GetEffected().GetLifeStats();
        int currentHp = lifeStats.GetCurrentHp();
        int currentMp = lifeStats.GetCurrentMp();

        // doesn't send sm_attack_status, checked on 4.5
        lifeStats.SetCurrentHp(currentMp, effect.GetEffector());
        lifeStats.SetCurrentMp(currentHp);
    }
}
