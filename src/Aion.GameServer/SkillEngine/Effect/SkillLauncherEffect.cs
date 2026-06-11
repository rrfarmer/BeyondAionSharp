using System.Xml.Serialization;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/SkillLauncherEffect : EffectTemplate. @XmlAttribute(name="skill_id")→[XmlAttribute("skill_id")]; applyEffect→SkillEngine.ApplyEffect; calculate→addSuccessEffect. EffectTemplate/Effect/SkillEngine red-tolerated.</summary>
[XmlType("SkillLauncherEffect")]
public class SkillLauncherEffect : EffectTemplate
{
    [XmlAttribute("skill_id")]
    protected int skillId;

    public override void ApplyEffect(Effect effect)
    {
        SkillEngine.GetInstance().ApplyEffect(skillId, effect.GetEffector(), effect.GetEffected());
    }

    public override void Calculate(Effect effect)
    {
        effect.AddSuccessEffect(this);
    }
}
