using System.Xml.Serialization;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/SlowEffect (ATracer) : BufEffect. applyEffect→addToEffectedController; calculate→base.Calculate(effect, SLOW_RESISTANCE, null); start/end set/unset AbnormalState.SLOW. StatEnum/AbnormalState red-tolerated.</summary>
[XmlType("SlowEffect")]
public class SlowEffect : BufEffect
{
    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void Calculate(Effect effect)
    {
        base.Calculate(effect, StatEnum.SLOW_RESISTANCE, null);
    }

    public override void StartEffect(Effect effect)
    {
        base.StartEffect(effect);
        effect.SetAbnormal(AbnormalState.SLOW);
        effect.GetEffected().GetEffectController().SetAbnormal(AbnormalState.SLOW);
    }

    public override void EndEffect(Effect effect)
    {
        base.EndEffect(effect);
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.SLOW);
    }
}
