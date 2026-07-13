using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Ends player flight unless invulnerable-wing immunity rejects the effect during calculation.</summary>
[XmlType("FallEffect")]
public class FallEffect : EffectTemplate
{
    protected override bool IsDodgedOrResisted(Effect effect, StatEnum? statEnum)
    {
        if (effect.GetEffected().GetEffectController().IsInAnyAbnormalState(AbnormalState.INVULNERABLE_WING))
            return true;
        return base.IsDodgedOrResisted(effect, statEnum);
    }

    public override void ApplyEffect(Effect effect)
    {
        if (effect.GetEffected() is Player player)
            player.GetFlyController().EndFly(true);
    }
}
