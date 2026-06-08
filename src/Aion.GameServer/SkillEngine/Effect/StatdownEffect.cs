namespace Aion.GameServer.SkillEngine.Effect;

/// <summary>
/// Java parity: skillengine/effect/StatdownEffect (ATracer).
/// </summary>
public class StatdownEffect : BufEffect
{
    public override void StartEffect(SkillEngine.Model.Effect effect)
    {
        base.StartEffect(effect);
        effect.GetEffected().GetLifeStats().UpdateCurrentStats();
    }

    // TODO bosses are resistent to this?
}
