namespace Aion.GameServer.SkillEngine.Effect;

/// <summary>Java parity: skillengine/effect/NoResurrectPenaltyEffect.</summary>
public class NoResurrectPenaltyEffect : BufEffect
{
    public override void Calculate(SkillEngine.Model.Effect effect)
    {
        effect.AddSuccessEffect(this);
    }
}
