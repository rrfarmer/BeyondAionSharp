using System.Xml.Serialization;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/DispelDebuffMentalEffect : AbstractDispelEffect. applyEffect→base.ApplyEffect(effect, DEBUFF_MENTAL, DEBUFF). EffectTemplate/Effect/enums red-tolerated.</summary>
[XmlType("DispelDebuffMentalEffect")]
public class DispelDebuffMentalEffect : AbstractDispelEffect
{
    public override void ApplyEffect(Effect effect)
    {
        base.ApplyEffect(effect, DispelCategoryType.DEBUFF_MENTAL, SkillTargetSlot.DEBUFF);
    }
}
