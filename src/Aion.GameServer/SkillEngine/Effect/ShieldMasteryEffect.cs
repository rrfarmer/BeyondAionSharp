using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/ShieldMasteryEffect (VladimirZ) : BufEffect. getModifiers→GetModifiers; per-modifier StatShieldMasteryFunction; size()>0→Count>0 guard; gameStats.addEffect. IStatFunction/StatShieldMasteryFunction red-tolerated.</summary>
[XmlType("ShieldMasteryEffect")]
public class ShieldMasteryEffect : BufEffect
{
    public override void StartEffect(Effect effect)
    {
        List<IStatFunction> modifiers = GetModifiers(effect);
        List<IStatFunction> masteryModifiers = new List<IStatFunction>();
        foreach (IStatFunction modifier in modifiers)
        {
            masteryModifiers.Add(new StatShieldMasteryFunction(modifier.GetName(), modifier.GetValue(), modifier.IsBonus()));
        }
        if (masteryModifiers.Count > 0)
        {
            effect.GetEffected().GetGameStats().AddEffect(effect, masteryModifiers);
        }
    }
}
