using System.Xml.Serialization;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/DispelDebuffEffect : AbstractDispelEffect. applyEffect→base.ApplyEffect(effect, ALL, DEBUFF). EffectTemplate/Effect/enums red-tolerated.</summary>
[XmlType("DispelDebuffEffect")]
public class DispelDebuffEffect : AbstractDispelEffect
{
    public override void ApplyEffect(Effect effect)
    {
        base.ApplyEffect(effect, DispelCategoryType.ALL, SkillTargetSlot.DEBUFF);
    }
}
