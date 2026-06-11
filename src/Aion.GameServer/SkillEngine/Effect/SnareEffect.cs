using System.Xml.Serialization;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/SnareEffect (ATracer) : BufEffect. applyEffect→addToEffectedController; calculate→base.Calculate(effect, SNARE_RESISTANCE, null); start/end set/unset AbnormalState.SNARE. StatEnum/AbnormalState red-tolerated.</summary>
[XmlType("SnareEffect")]
public class SnareEffect : BufEffect
{
    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void Calculate(Effect effect)
    {
        base.Calculate(effect, StatEnum.SNARE_RESISTANCE, null);
    }

    public override void EndEffect(Effect effect)
    {
        base.EndEffect(effect);
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.SNARE);
    }

    public override void StartEffect(Effect effect)
    {
        base.StartEffect(effect);
        effect.GetEffected().GetEffectController().SetAbnormal(AbnormalState.SNARE);
        effect.SetAbnormal(AbnormalState.SNARE);
    }
}
