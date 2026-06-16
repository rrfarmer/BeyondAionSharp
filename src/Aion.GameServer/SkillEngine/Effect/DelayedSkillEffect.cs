using System.Xml.Serialization;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/DelayedSkillEffect (kecimis, Cheatkiller) : EffectTemplate. @XmlAttribute(name="skill_id")→[XmlAttribute("skill_id")] (no @XmlType in Java); endEffect→base + applyEffectsDirectly if ended by time. EffectTemplate/Effect/SkillEngine red-tolerated.</summary>
public class DelayedSkillEffect : EffectTemplate
{
    [XmlAttribute("skill_id")]
    public int skillId;

    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void EndEffect(Effect effect)
    {
        base.EndEffect(effect);
        if (effect.IsEndedByTime())
            Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectsDirectly(skillId, effect.GetEffector(), effect.GetEffected(), effect.GetTargetX(), effect.GetTargetY(), effect.GetTargetZ());
    }
}
