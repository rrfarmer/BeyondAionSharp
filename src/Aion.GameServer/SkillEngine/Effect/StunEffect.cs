using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/StunEffect (ATracer) : EffectTemplate. @XmlType(name)→[XmlType]; super.calculate→base.Calculate; instanceof Player→is Player. EffectTemplate/Effect red-tolerated.</summary>
[XmlType("StunEffect")]
public class StunEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void Calculate(Effect effect)
    {
        base.Calculate(effect, StatEnum.STUN_RESISTANCE, null);
    }

    public override void StartEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();
        effected.GetController().CancelCurrentSkill(effect.GetEffector());
        if (effected is Player player)
        {
            player.GetFlyController().OnStopGliding();
            player.GetMoveController().AbortMove();
        }
        effect.GetEffected().GetEffectController().SetAbnormal(AbnormalState.STUN);
        effect.SetAbnormal(AbnormalState.STUN);
    }

    public override void EndEffect(Effect effect)
    {
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.STUN);
    }
}
