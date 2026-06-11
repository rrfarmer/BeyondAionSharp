using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/DispelNpcBuffEffect : AbstractDispelEffect. applyEffect→base.ApplyEffect(effect, NPC_BUFF, BUFF). EffectTemplate/Effect/enums red-tolerated.</summary>
[XmlType("DispelNpcBuffEffect")]
public class DispelNpcBuffEffect : AbstractDispelEffect
{
    public override void ApplyEffect(Effect effect)
    {
        base.ApplyEffect(effect, DispelCategoryType.NPC_BUFF, SkillTargetSlot.BUFF);
    }
}
