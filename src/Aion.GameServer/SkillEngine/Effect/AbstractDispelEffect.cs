using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/AbstractDispelEffect (kecimis) abstract : EffectTemplate. @XmlAttribute(name="dispel_level")→[XmlAttribute("dispel_level")]; 3-arg applyEffect overload helper called by subclasses' applyEffect(Effect). Inherited CalculateBaseValue + EffectTemplate/Effect/DispelCategoryType/SkillTargetSlot red-tolerated.</summary>
[XmlType("AbstractDispelEffect")]
public abstract class AbstractDispelEffect : EffectTemplate
{
    [XmlAttribute]
    public int dpower;
    [XmlAttribute]
    public int power;
    [XmlAttribute("dispel_level")]
    public int dispelLevel;

    public void ApplyEffect(Effect effect, DispelCategoryType type, SkillTargetSlot slot)
    {
        int count = CalculateBaseValue(effect);
        int finalPower = power + dpower * effect.GetSkillLevel();

        effect.GetEffected().GetEffectController().RemoveEffectByDispelCat(type, slot, count, dispelLevel, finalPower);
    }
}
