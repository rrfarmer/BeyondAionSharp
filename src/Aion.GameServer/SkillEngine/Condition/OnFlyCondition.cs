using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Condition;

/// <summary>
/// Java parity: skillengine/condition/OnFlyCondition (ATracer).
/// </summary>
public class OnFlyCondition : Condition
{
    public override bool Validate(Skill env)
    {
        return env.GetEffector().IsInFlyingState();
    }

    public override bool Validate(Stat2 stat, IStatFunction statFunction)
    {
        return stat.GetOwner().IsInFlyingState();
    }

    public override bool Validate(Effect effect)
    {
        return effect.GetEffected().IsInFlyingState();
    }
}
