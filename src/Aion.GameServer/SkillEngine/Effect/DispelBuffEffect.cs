using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/DispelBuffEffect : AbstractDispelEffect. applyEffect→base.ApplyEffect(effect, BUFF, BUFF). EffectTemplate/Effect/enums red-tolerated.</summary>
[XmlType("DispelBuffEffect")]
public class DispelBuffEffect : AbstractDispelEffect
{
    public override void ApplyEffect(Effect effect)
    {
        base.ApplyEffect(effect, DispelCategoryType.BUFF, SkillTargetSlot.BUFF);
    }
}
