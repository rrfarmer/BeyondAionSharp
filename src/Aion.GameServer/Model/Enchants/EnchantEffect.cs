using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Stats.Container;

namespace Aion.GameServer.Model.Enchants;

/// <summary>Java parity: model/enchants/EnchantEffect implements StatOwner.</summary>
public class EnchantEffect : Aion.GameServer.Model.Stats.Calc.IStatOwner
{
    private ItemSlot itemSlot;

    public EnchantEffect(Item item, Aion.GameServer.Model.GameObjects.Player.Player player, List<EnchantStat> enchantStats)
    {
        List<Aion.GameServer.Model.Stats.Calc.Functions.IStatFunction> functions = new List<Aion.GameServer.Model.Stats.Calc.Functions.IStatFunction>();
        long itemSlot = item.GetEquipmentSlot();
        foreach (EnchantStat enchantStat in enchantStats)
        {
            switch (enchantStat.GetStat())
            {
                case StatEnum.PHYSICAL_ATTACK:
                case StatEnum.MAGICAL_ATTACK:
                    if (itemSlot == ItemSlot.MAIN_HAND.GetSlotIdMask() || itemSlot == ItemSlot.MAIN_OR_SUB.GetSlotIdMask())
                        this.itemSlot = ItemSlot.MAIN_HAND;
                    else
                        this.itemSlot = ItemSlot.SUB_HAND;
                    functions.Add(new Aion.GameServer.Model.Stats.Calc.Functions.StatAddFunction(enchantStat.GetStat(), enchantStat.GetValue(), false));
                    break;
                case StatEnum.BOOST_MAGICAL_SKILL:
                    if (itemSlot == ItemSlot.MAIN_HAND.GetSlotIdMask() || itemSlot == ItemSlot.MAIN_OR_SUB.GetSlotIdMask())
                        functions.Add(new Aion.GameServer.Model.Stats.Calc.Functions.StatAddFunction(enchantStat.GetStat(), enchantStat.GetValue(), false));
                    break;
                default:
                    functions.Add(new Aion.GameServer.Model.Stats.Calc.Functions.StatAddFunction(enchantStat.GetStat(), enchantStat.GetValue(), false));
                    break;
            }
        }
        player.GetGameStats().AddEffect(this, functions);
    }

    public void EndEffect(Aion.GameServer.Model.GameObjects.Player.Player player)
    {
        player.GetGameStats().EndEffect(this);
    }

    public ItemSlot GetItemSlot()
    {
        return itemSlot;
    }
}
