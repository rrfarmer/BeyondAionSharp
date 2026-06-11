using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Condition;

/// <summary>
/// Java parity: skillengine/condition/NoFlyingCondition (Sippolo).
/// </summary>
public class NoFlyingCondition : Condition
{
    public override bool Validate(Skill env)
    {
        return !env.GetEffector().IsFlying();
    }

    public override bool Validate(Effect effect)
    {
        return !effect.GetEffected().IsFlying();
    }
}
