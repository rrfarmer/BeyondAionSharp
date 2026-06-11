using System.Xml.Serialization;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Skillengine.Change;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

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
                foreach (Change c in change)
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
