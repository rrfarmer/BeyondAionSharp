using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/FallEffect : EffectTemplate. instanceof Player→is Player; super.calculate(effect,null,null)→base.Calculate; ends fly unless INVULNERABLE_WING. EffectTemplate/Effect/flyController red-tolerated.</summary>
[XmlType("FallEffect")]
public class FallEffect : EffectTemplate
{
    public override void Calculate(Effect effect)
    {
        // Affects only players (for now as we dont have flying Npc's)
        if (effect.GetEffected() is Player)
            base.Calculate(effect, null, null);
    }

    public override void ApplyEffect(Effect effect)
    {
        if (!effect.GetEffected().GetEffectController().IsInAnyAbnormalState(AbnormalState.INVULNERABLE_WING))
            ((Player)effect.GetEffected()).GetFlyController().EndFly(true);
    }
}
