namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/XPBoostEffect.</summary>
public class XPBoostEffect : BufEffect
{
    public override void Calculate(SkillEngine.Model.Effect effect)
    {
        effect.AddSuccessEffect(this);
    }
}
