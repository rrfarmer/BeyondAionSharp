using System.Xml.Serialization;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/DeformEffect (ATracer) : TransformEffect. super.calculate(effect, StatEnum.DEFORM_RESISTANCE, null)→base.Calculate(effect, StatEnum.DEFORM_RESISTANCE, null) (3-arg overload, SpellStatus null); effectController.setAbnormal/unsetAbnormal(AbnormalState.DEFORM); effect.setAbnormal. StatEnum/AbnormalState red-tolerated.</summary>
[XmlType("DeformEffect")]
public class DeformEffect : TransformEffect
{
    public override void ApplyEffect(Effect effect)
    {
        base.ApplyEffect(effect);
    }

    public override void Calculate(Effect effect)
    {
        base.Calculate(effect, StatEnum.DEFORM_RESISTANCE, null);
    }

    public override void StartEffect(Effect effect)
    {
        base.StartEffect(effect);
        effect.GetEffected().GetEffectController().SetAbnormal(AbnormalState.DEFORM);
        effect.SetAbnormal(AbnormalState.DEFORM);
    }

    public override void EndEffect(Effect effect)
    {
        base.EndEffect(effect);
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.DEFORM);
    }
}
