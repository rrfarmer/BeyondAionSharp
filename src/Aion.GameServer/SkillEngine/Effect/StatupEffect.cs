namespace Aion.GameServer.SkillEngine.Effect;

/// <summary>
/// Java parity: skillengine/effect/StatupEffect (ATracer).
/// </summary>
public class StatupEffect : BufEffect
{
    public override void EndEffect(SkillEngine.Model.Effect effect)
    {
        base.EndEffect(effect);
        effect.GetEffected().GetLifeStats().UpdateCurrentStats();
    }
}
