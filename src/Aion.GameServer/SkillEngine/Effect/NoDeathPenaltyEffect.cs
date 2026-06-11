namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/NoDeathPenaltyEffect.</summary>
public class NoDeathPenaltyEffect : BufEffect
{
    public override void Calculate(SkillEngine.Model.Effect effect)
    {
        effect.AddSuccessEffect(this);
    }
}
