using System.Xml.Serialization;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/DispelNpcDebuffEffect : AbstractDispelEffect. applyEffect→base.ApplyEffect(effect, NPC_DEBUFF_PHYSICAL, DEBUFF). EffectTemplate/Effect/enums red-tolerated.</summary>
[XmlType("DispelNpcDebuffEffect")]
public class DispelNpcDebuffEffect : AbstractDispelEffect
{
    public override void ApplyEffect(Effect effect)
    {
        base.ApplyEffect(effect, DispelCategoryType.NPC_DEBUFF_PHYSICAL, SkillTargetSlot.DEBUFF);
    }
}
