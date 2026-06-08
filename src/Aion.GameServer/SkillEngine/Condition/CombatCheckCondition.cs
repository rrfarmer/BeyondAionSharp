using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.condition;

/// <summary>
/// Java parity: skillengine/condition/CombatCheckCondition (nrg).
/// </summary>
public class CombatCheckCondition : Condition
{
    public override bool Validate(Skill skill)
    {
        if (skill.GetEffector() is Player)
        {
            return !((Player)skill.GetEffector()).GetController().IsInCombat();
        }
        return true;
    }
}
