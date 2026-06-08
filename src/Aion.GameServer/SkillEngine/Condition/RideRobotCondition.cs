using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.condition;

/// <summary>
/// Java parity: skillengine/condition/RideRobotCondition (Cheatkiller).
/// </summary>
public class RideRobotCondition : Condition
{
    public override bool Validate(Skill skill)
    {
        if (skill.GetEffector() is Player)
        {
            return ((Player)skill.GetEffector()).IsInRobotMode();
        }
        else
        {
            return true;
        }
    }
}
