using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Condition;

/// <summary>
/// Java parity: skillengine/condition/TargetFlyingCondition (Sippolo, kecimis).
/// </summary>
public class TargetFlyingCondition : Condition
{
    [XmlAttribute("restriction")]
    public FlyingRestriction Restriction = FlyingRestriction.FLY;

    public override bool Validate(Skill env)
    {
        if (env.GetFirstTarget() == null)
            return false;

        switch (Restriction)
        {
            case FlyingRestriction.FLY:
                return env.GetFirstTarget().IsFlying();
            case FlyingRestriction.GROUND:
                return !env.GetFirstTarget().IsFlying();
        }

        return true;
    }

    public override bool Validate(Effect effect)
    {
        if (effect.GetEffected() == null)
            return false;

        switch (Restriction)
        {
            case FlyingRestriction.FLY:
                return effect.GetEffected().IsFlying();
            case FlyingRestriction.GROUND:
                return !effect.GetEffected().IsFlying();
        }

        return true;
    }
}
