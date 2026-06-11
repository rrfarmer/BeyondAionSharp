using System.Xml.Serialization;
using AbnormalState = Aion.GameServer.SkillEngine.Effects.AbnormalState;

namespace Aion.GameServer.SkillEngine.Effects.Modifier;

/// <summary>
/// Java parity: skillengine/effect/modifier/AbnormalDamageModifier (kecimis).
/// </summary>
public class AbnormalDamageModifier : ActionModifier
{
    [XmlAttribute("state")]
    public AbnormalState State;

    public override int Analyze(SkillEngine.Model.Effect effect)
    {
        return Value + effect.GetSkillLevel() * Delta;
    }

    public override bool Check(SkillEngine.Model.Effect effect)
    {
        return effect.GetEffected().GetEffectController().IsAbnormalSet(State);
    }
}
