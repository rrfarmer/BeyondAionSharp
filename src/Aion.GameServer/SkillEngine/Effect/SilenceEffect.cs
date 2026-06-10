using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/SilenceEffect (ATracer) : EffectTemplate. @XmlType(name)→[XmlType]; super.calculate→base.Calculate; getType()→GetType_(). EffectTemplate/Effect red-tolerated.</summary>
[XmlType("SilenceEffect")]
public class SilenceEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void Calculate(Effect effect)
    {
        base.Calculate(effect, StatEnum.SILENCE_RESISTANCE, null);
    }

    public override void StartEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();
        effect.SetAbnormal(AbnormalState.SILENCE);
        effected.GetEffectController().SetAbnormal(AbnormalState.SILENCE);
        if (effected.GetCastingSkill() != null && effected.GetCastingSkill().GetSkillTemplate().GetType_() == SkillType.MAGICAL)
            effected.GetController().CancelCurrentSkill(effect.GetEffector());
    }

    public override void EndEffect(Effect effect)
    {
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.SILENCE);
    }
}
