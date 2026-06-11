namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/NoResurrectPenaltyEffect.</summary>
public class NoResurrectPenaltyEffect : BufEffect
{
    public override void Calculate(Aion.GameServer.SkillEngine.Model.Effect effect)
    {
        effect.AddSuccessEffect(this);
    }
}
