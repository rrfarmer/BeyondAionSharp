using Aion.GameServer.Services;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.condition;

/// <summary>
/// Java parity: skillengine/condition/BackCondition (kecimis).
/// </summary>
public class BackCondition : Condition
{
    public override bool Validate(Skill env)
    {
        if (env.GetFirstTarget() == null || env.GetEffector() == null)
            return false;

        return PositionUtil.IsBehind(env.GetEffector(), env.GetFirstTarget());
    }

    public override bool Validate(Effect effect)
    {
        if (effect.GetEffected() == null || effect.GetEffector() == null)
            return false;

        return PositionUtil.IsBehind(effect.GetEffector(), effect.GetEffected());
    }
}
