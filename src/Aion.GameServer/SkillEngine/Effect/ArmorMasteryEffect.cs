using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/ArmorMasteryEffect (ATracer) : BufEffect. @XmlAttribute(name="armor")→[XmlAttribute("armor")] ItemSubType; change==null guard; CalculateBaseValue; getModifiers→GetModifiers; per-modifier StatArmorMasteryFunction with equipped items; gameStats.addEffect. IStatFunction/StatArmorMasteryFunction red-tolerated.</summary>
[XmlType("ArmorMasteryEffect")]
public class ArmorMasteryEffect : BufEffect
{
    [XmlAttribute("armor")]
    private ItemSubType armorType;

    public override void StartEffect(Effect effect)
    {
        if (change == null)
            return;
        int fixedBonus = CalculateBaseValue(effect);
        List<IStatFunction> modifiers = GetModifiers(effect);
        List<IStatFunction> masteryModifiers = new List<IStatFunction>();
        List<Item> equipment = ((Player)effect.GetEffected()).GetEquipment().GetEquippedItems();
        foreach (IStatFunction modifier in modifiers)
        {
            masteryModifiers.Add(new StatArmorMasteryFunction(armorType, modifier.GetName(), modifier.GetValue(), modifier.IsBonus(), fixedBonus, equipment));
        }
        effect.GetEffected().GetGameStats().AddEffect(effect, masteryModifiers);
    }
}
