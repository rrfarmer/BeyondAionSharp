using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/BindEffect (ATracer) : EffectTemplate. @XmlType(name)→[XmlType]; super.calculate→base.Calculate; getType()→GetType_(); AbnormalState same-namespace. EffectTemplate/Effect red-tolerated.</summary>
[XmlType("BindEffect")]
public class BindEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void Calculate(Effect effect)
    {
        base.Calculate(effect, StatEnum.BIND_RESISTANCE, null);
    }

    public override void StartEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();
        effect.SetAbnormal(AbnormalState.BIND);
        effected.GetEffectController().SetAbnormal(AbnormalState.BIND);
        if (effected.GetCastingSkill() != null && effected.GetCastingSkill().GetSkillTemplate().GetType_() == SkillType.PHYSICAL)
            effected.GetController().CancelCurrentSkill(effect.GetEffector());
    }

    public override void EndEffect(Effect effect)
    {
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.BIND);
    }
}
