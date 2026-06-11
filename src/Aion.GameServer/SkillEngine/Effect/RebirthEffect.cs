using System.Xml.Serialization;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/RebirthEffect (Sarynth) : EffectTemplate. @XmlAttribute(name="resurrect_percent")/("skill_id"); applyEffect→addToEffectedController; getResurrectPercent/getSkillId getters (getSkillId shadows? Java effect.getSkillId is on Effect; here this.skillId getter). Effect red-tolerated.</summary>
[XmlType("RebirthEffect")]
public class RebirthEffect : EffectTemplate
{
    [XmlAttribute("resurrect_percent")]
    protected int resurrectPercent;

    [XmlAttribute("skill_id")]
    protected int skillId;

    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public int GetResurrectPercent()
    {
        return resurrectPercent;
    }

    public int GetSkillId()
    {
        return skillId;
    }
}
