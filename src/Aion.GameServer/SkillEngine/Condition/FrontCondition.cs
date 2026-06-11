using Aion.GameServer.Services;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Condition;

/// <summary>
/// Java parity: skillengine/condition/FrontCondition (Rolandas).
/// </summary>
public class FrontCondition : Condition
{
    public override bool Validate(Skill env)
    {
        if (env.GetFirstTarget() == null || env.GetEffector() == null)
            return false;

        return PositionUtil.IsInFrontOf(env.GetEffector(), env.GetFirstTarget());
    }

    public override bool Validate(Effect effect)
    {
        if (effect.GetEffected() == null || effect.GetEffector() == null)
            return false;

        return PositionUtil.IsInFrontOf(effect.GetEffector(), effect.GetEffected());
    }
}
