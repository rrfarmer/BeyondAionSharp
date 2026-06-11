namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>
/// Java parity: skillengine/effect/StatupEffect (ATracer).
/// </summary>
public class StatupEffect : BufEffect
{
    public override void EndEffect(Aion.GameServer.SkillEngine.Model.Effect effect)
    {
        base.EndEffect(effect);
        effect.GetEffected().GetLifeStats().UpdateCurrentStats();
    }
}
