namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>
/// Java parity: skillengine/effect/StatdownEffect (ATracer).
/// </summary>
public class StatdownEffect : BufEffect
{
    public override void StartEffect(Aion.GameServer.SkillEngine.Model.Effect effect)
    {
        base.StartEffect(effect);
        effect.GetEffected().GetLifeStats().UpdateCurrentStats();
    }

    // TODO bosses are resistent to this?
}
