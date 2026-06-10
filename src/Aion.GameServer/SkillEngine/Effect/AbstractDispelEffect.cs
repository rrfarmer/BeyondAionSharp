using System.Xml.Serialization;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/AbstractDispelEffect (kecimis) abstract : EffectTemplate. @XmlAttribute(name="dispel_level")→[XmlAttribute("dispel_level")]; 3-arg applyEffect overload helper called by subclasses' applyEffect(Effect). Inherited CalculateBaseValue + EffectTemplate/Effect/DispelCategoryType/SkillTargetSlot red-tolerated.</summary>
[XmlType("AbstractDispelEffect")]
public abstract class AbstractDispelEffect : EffectTemplate
{
    [XmlAttribute]
    protected int dpower;
    [XmlAttribute]
    protected int power;
    [XmlAttribute("dispel_level")]
    protected int dispelLevel;

    public void ApplyEffect(Effect effect, DispelCategoryType type, SkillTargetSlot slot)
    {
        int count = CalculateBaseValue(effect);
        int finalPower = power + dpower * effect.GetSkillLevel();

        effect.GetEffected().GetEffectController().RemoveEffectByDispelCat(type, slot, count, dispelLevel, finalPower);
    }
}
