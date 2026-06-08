using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.condition;

/// <summary>
/// Java parity: skillengine/condition/SkillChargeCondition (Rolandas).
/// </summary>
public class SkillChargeCondition : ChargeCondition
{
    public override bool Validate(Skill env)
    {
        return true;
    }

    public int GetValue()
    {
        return value;
    }
}
