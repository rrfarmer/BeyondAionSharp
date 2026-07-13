using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Applies no-flight state unless invulnerable-wing immunity rejects the effect during calculation.</summary>
[XmlType("NoFlyEffect")]
public class NoFlyEffect : EffectTemplate
{
    public override void Calculate(Effect effect)
    {
        base.Calculate(effect, StatEnum.NOFLY_RESISTANCE, null);
    }

    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    protected override bool IsDodgedOrResisted(Effect effect, StatEnum? statEnum)
    {
        if (effect.GetEffected().GetEffectController().IsInAnyAbnormalState(AbnormalState.INVULNERABLE_WING))
            return true;
        return base.IsDodgedOrResisted(effect, statEnum);
    }

    public override void StartEffect(Effect effect)
    {
        if (effect.GetEffected() is Player player)
            player.GetFlyController().EndFly(true);

        effect.SetAbnormal(AbnormalState.NOFLY);
        effect.GetEffected().GetEffectController().SetAbnormal(AbnormalState.NOFLY);
    }

    public override void EndEffect(Effect effect)
    {
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.NOFLY);
    }
}
