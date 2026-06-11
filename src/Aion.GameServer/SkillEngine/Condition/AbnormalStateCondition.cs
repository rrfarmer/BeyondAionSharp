using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.Model;
using AbnormalState = Aion.GameServer.SkillEngine.Effects.AbnormalState;

namespace Aion.GameServer.SkillEngine.Condition;

/// <summary>
/// Java parity: skillengine/condition/AbnormalStateCondition (kecimis).
/// </summary>
public class AbnormalStateCondition : Condition
{
    [XmlAttribute("value")]
    public AbnormalState Value;

    public override bool Validate(Skill env)
    {
        if (env.GetFirstTarget() != null)
            return env.GetFirstTarget().GetEffectController().IsAbnormalSet(Value);
        return false;
    }

    public override bool Validate(Effect effect)
    {
        if (effect.GetEffected() != null)
            return effect.GetEffected().GetEffectController().IsAbnormalSet(Value);
        return false;
    }
}
