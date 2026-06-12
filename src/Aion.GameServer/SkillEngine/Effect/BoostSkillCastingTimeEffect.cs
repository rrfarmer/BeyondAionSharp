using System.Xml.Serialization;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Change;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/BoostSkillCastingTimeEffect (ATracer) : BufEffect. calculate override: isEnemy + change has c.getValue()&lt;0 → base.Calculate(effect, SLOW_RESISTANCE, null) early return, else base.Calculate(effect). Change/StatEnum red-tolerated.</summary>
[XmlType("BoostSkillCastingTimeEffect")]
public class BoostSkillCastingTimeEffect : BufEffect
{
    public override void Calculate(Effect effect)
    {
        if (effect.GetEffected().IsEnemy(effect.GetEffector()))
        {
            if (change != null)
            {
                foreach (Aion.GameServer.SkillEngine.Change.Change c in change)
                {
                    if (c.GetValue() < 0)
                    {
                        base.Calculate(effect, StatEnum.SLOW_RESISTANCE, null);
                        return;
                    }
                }
            }
        }
        base.Calculate(effect);
    }
}
