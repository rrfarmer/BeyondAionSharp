using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Item.Enums;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/WeaponMasteryEffect (ATracer) : BufEffect. @XmlAttribute(name="weapon")→[XmlAttribute("weapon")] ItemGroup; change==null guard; itemGroup.getItemSubType()==TWO_HAND→single fn else PHYSICAL/MAGICAL_ATTACK→MAIN_HAND_POWER+OFF_HAND_POWER; gameStats.addEffect. IStatFunction/StatWeaponMasteryFunction/ItemGroup red-tolerated.</summary>
[XmlType("WeaponMasteryEffect")]
public class WeaponMasteryEffect : BufEffect
{
    [XmlAttribute("weapon")]
    private ItemGroup itemGroup;

    public override void StartEffect(Effect effect)
    {
        if (change == null)
            return;

        List<IStatFunction> modifiers = GetModifiers(effect);
        List<IStatFunction> masteryModifiers = new List<IStatFunction>();
        foreach (IStatFunction modifier in modifiers)
        {
            if (itemGroup.GetItemSubType() == ItemSubType.TWO_HAND)
            {
                masteryModifiers.Add(new StatWeaponMasteryFunction(itemGroup, modifier.GetName(), modifier.GetValue(), modifier.IsBonus()));
            }
            else if (modifier.GetName() == StatEnum.PHYSICAL_ATTACK || modifier.GetName() == StatEnum.MAGICAL_ATTACK)
            {
                masteryModifiers.Add(new StatWeaponMasteryFunction(itemGroup, StatEnum.MAIN_HAND_POWER, modifier.GetValue(), modifier.IsBonus()));
                masteryModifiers.Add(new StatWeaponMasteryFunction(itemGroup, StatEnum.OFF_HAND_POWER, modifier.GetValue(), modifier.IsBonus()));
            }
        }
        effect.GetEffected().GetGameStats().AddEffect(effect, masteryModifiers);
    }
}
