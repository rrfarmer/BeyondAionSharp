using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Model.Stats.Calc.Functions;

/// <summary>Java parity: model/stats/calc/functions/StatArmorMasteryFunction (ATracer). : StatRateFunction. switch-expr on ItemSlot→C# switch expr; CalculationType...→params; base `value` field→Value; truncating int division preserved. ItemSlot/Item/Stat2 red-tolerated.</summary>
public class StatArmorMasteryFunction : StatRateFunction
{
    private readonly ItemSubType armorType;
    private readonly int fixedBonus;
    private int equipmentFactor;

    public StatArmorMasteryFunction(ItemSubType armorType, StatEnum name, int value, bool bonus, int fixedBonus, List<Item> equipment) : base(name, value, bonus)
    {
        this.armorType = armorType;
        this.fixedBonus = fixedBonus;
        UpdateEquipmentFactor(equipment);
    }

    public void UpdateEquipmentFactor(List<Item> equipment)
    {
        equipmentFactor = 0;
        foreach (Item item in equipment)
        {
            if (item.GetItemTemplate().GetItemSubType() == armorType)
            {
                equipmentFactor += GetEquipmentFactor(ItemSlot.GetSlotFor(item.GetEquipmentSlot()));
            }
        }
    }

    private int GetEquipmentFactor(ItemSlot itemSlot)
    {
        return itemSlot switch
        {
            ItemSlot.TORSO => 30,
            ItemSlot.PANTS => 25,
            ItemSlot.SHOULDER or ItemSlot.GLOVES or ItemSlot.BOOTS => 15,
            _ => 0,
        };
    }

    public override void Apply(Stat2 stat, params CalculationType[] calculationTypes)
    {
        base.Apply(stat, calculationTypes);
        if (fixedBonus != 0 && equipmentFactor != 0)
            stat.AddToBonus(fixedBonus * equipmentFactor / 100f);
    }

    public override int GetValue()
    {
        return Value * equipmentFactor / 100; // truncation from equipmentFactor is retail-like
    }
}
