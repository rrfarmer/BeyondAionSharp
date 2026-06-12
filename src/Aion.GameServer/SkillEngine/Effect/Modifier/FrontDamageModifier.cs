using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.SkillEngine.Effects.Modifier;

/// <summary>
/// Java parity: skillengine/effect/modifier/FrontDamageModifier (ATracer).
/// </summary>
public class FrontDamageModifier : ActionModifier
{
    public override int Analyze(Aion.GameServer.SkillEngine.Model.Effect effect)
    {
        return Value + effect.GetSkillLevel() * Delta;
    }

    public override bool Check(Aion.GameServer.SkillEngine.Model.Effect effect)
    {
        return PositionUtil.IsInFrontOf(effect.GetEffector(), effect.GetEffected());
    }
}
