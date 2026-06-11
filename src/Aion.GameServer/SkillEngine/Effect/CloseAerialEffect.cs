using System.Xml.Serialization;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/CloseAerialEffect : EffectTemplate. applyEffect→removeEffect(8224); super.calculate(effect,null,SpellStatus.CLOSEAERIAL)→base.Calculate. EffectTemplate/Effect/SpellStatus red-tolerated.</summary>
[XmlType("CloseAerialEffect")]
public class CloseAerialEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        effect.GetEffected().GetEffectController().RemoveEffect(8224);
    }

    public override void Calculate(Effect effect)
    {
        base.Calculate(effect, null, SpellStatus.CLOSEAERIAL);
    }
}
