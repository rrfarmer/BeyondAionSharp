using Aion.GameServer.Services;

namespace Aion.GameServer.SkillEngine.Effect.Modifier;

/// <summary>
/// Java parity: skillengine/effect/modifier/FrontDamageModifier (ATracer).
/// </summary>
public class FrontDamageModifier : ActionModifier
{
    public override int Analyze(SkillEngine.Model.Effect effect)
    {
        return Value + effect.GetSkillLevel() * Delta;
    }

    public override bool Check(SkillEngine.Model.Effect effect)
    {
        return PositionUtil.IsInFrontOf(effect.GetEffector(), effect.GetEffected());
    }
}
