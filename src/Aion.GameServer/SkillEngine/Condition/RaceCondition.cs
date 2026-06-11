using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Condition;

/// <summary>
/// Java parity: skillengine/condition/RaceCondition (kecimis).
/// </summary>
public class RaceCondition : Condition
{
    [XmlAttribute("race")]
    public List<Race>? races;

    public override bool Validate(Skill env)
    {
        if (env.GetFirstTarget() == null || env.GetEffector() == null)
            return false;

        bool result = false;
        foreach (Race race in races!)
        {
            if (race == env.GetFirstTarget().GetRace())
                result = true;
        }

        return result;
    }

    public override bool Validate(Effect effect)
    {
        if (effect.GetEffected() == null || effect.GetEffector() == null)
            return false;

        bool result = false;
        foreach (Race race in races!)
        {
            if (race == effect.GetEffected().GetRace())
                result = true;
        }

        return result;
    }
}
