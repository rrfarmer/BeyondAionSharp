using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Condition;

/// <summary>
/// Java parity: skillengine/condition/SelfFlyingCondition (kecimis).
/// </summary>
public class SelfFlyingCondition : Condition
{
    [XmlAttribute("restriction")]
    public FlyingRestriction Restriction;

    public override bool Validate(Skill env)
    {
        if (env.GetEffector() == null)
            return false;

        switch (Restriction)
        {
            case FlyingRestriction.FLY:
                return env.GetEffector().IsInFlyingState();
            case FlyingRestriction.GROUND:
                return !env.GetEffector().IsInFlyingState();
        }

        return true;
    }

    public override bool Validate(Effect effect)
    {
        if (effect.GetEffector() == null)
            return false;

        switch (Restriction)
        {
            case FlyingRestriction.FLY:
                return effect.GetEffector().IsInFlyingState();
            case FlyingRestriction.GROUND:
                return !effect.GetEffector().IsInFlyingState();
        }

        return true;
    }
}
