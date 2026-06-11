using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/DiseaseEffect (kecimis) : EffectTemplate. calculate→base.Calculate(effect, DISEASE_RESISTANCE, null); applyEffect→addToEffectedController (skillId 18386); start set AbnormalState.DISEASE; end isAbnormalSet guard→unset. StatEnum/AbnormalState red-tolerated.</summary>
[XmlType("DiseaseEffect")]
public class DiseaseEffect : EffectTemplate
{
    public override void Calculate(Effect effect)
    {
        base.Calculate(effect, StatEnum.DISEASE_RESISTANCE, null);
    }

    // skillId 18386
    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void StartEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();
        effect.SetAbnormal(AbnormalState.DISEASE);
        effected.GetEffectController().SetAbnormal(AbnormalState.DISEASE);
    }

    public override void EndEffect(Effect effect)
    {
        if (effect.GetEffected().GetEffectController().IsAbnormalSet(AbnormalState.DISEASE))
            effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.DISEASE);
    }
}
