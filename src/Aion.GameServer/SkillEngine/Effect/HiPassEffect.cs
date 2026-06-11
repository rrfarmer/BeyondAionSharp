namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/HiPassEffect.</summary>
public class HiPassEffect : BufEffect
{
    public override void Calculate(Aion.GameServer.SkillEngine.Model.Effect effect)
    {
        effect.AddSuccessEffect(this);
    }
}
