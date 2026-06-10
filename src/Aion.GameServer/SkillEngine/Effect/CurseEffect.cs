using System.Xml.Serialization;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/CurseEffect (ATracer) : BufEffect. calculate→base.Calculate(effect, CURSE_RESISTANCE, null); start/end set/unset AbnormalState.CURSE. StatEnum/AbnormalState red-tolerated.</summary>
[XmlType("CurseEffect")]
public class CurseEffect : BufEffect
{
    public override void Calculate(Effect effect)
    {
        base.Calculate(effect, StatEnum.CURSE_RESISTANCE, null);
    }

    public override void StartEffect(Effect effect)
    {
        base.StartEffect(effect);
        effect.SetAbnormal(AbnormalState.CURSE);
        effect.GetEffected().GetEffectController().SetAbnormal(AbnormalState.CURSE);
    }

    public override void EndEffect(Effect effect)
    {
        base.EndEffect(effect);
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.CURSE);
    }
}
