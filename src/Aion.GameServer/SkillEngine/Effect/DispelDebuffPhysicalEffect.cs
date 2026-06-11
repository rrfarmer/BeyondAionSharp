using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/DispelDebuffPhysicalEffect : AbstractDispelEffect. applyEffect→base.ApplyEffect(effect, DEBUFF_PHYSICAL, DEBUFF). EffectTemplate/Effect/enums red-tolerated.</summary>
[XmlType("DispelDebuffPhysicalEffect")]
public class DispelDebuffPhysicalEffect : AbstractDispelEffect
{
    public override void ApplyEffect(Effect effect)
    {
        base.ApplyEffect(effect, DispelCategoryType.DEBUFF_PHYSICAL, SkillTargetSlot.DEBUFF);
    }
}
