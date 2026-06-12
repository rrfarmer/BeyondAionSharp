using System;
using System.Collections.Generic;
using Aion.Commons.Nio;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Items.Enums;
using ItemBlobType = global::Aion.GameServer.Network.Aion.Iteminfo.ItemInfoBlob.ItemBlobType;

namespace Aion.GameServer.Network.Aion.Iteminfo;

/// <summary>
/// Java parity: network/aion/iteminfo/EnchantInfoBlobEntry (-Nemesiss-, Rolandas). Sends enchantment, bonus attributes, mana stones,
/// god stone etc. Collectors.toMap -> Dictionary; Collections.emptyMap -> empty Dictionary. Item/ManaStone/ItemStone/IdianStone/
/// PlumStatEnum/ItemGroup red-tolerated.
/// </summary>
public class EnchantInfoBlobEntry : ItemBlobEntry
{
    public static int SIZE = 138; // 8 + Item.MAX_BASIC_STONES * 4 + 4 + 13 + 5 + 5 + (18 * 4 + 1)

    internal EnchantInfoBlobEntry() : base(ItemBlobType.ENCHANT_INFO)
    {
    }

    public override void WriteThisBlob(ByteBuffer buf)
    {
        WriteInfo(buf, ownerItem);
    }

    public static void WriteInfo(ByteBuffer buf, Item item)
    {
        WriteInfo(buf, item, !item.IsIdentified() ? -1 : item.GetOptionalSockets(), !item.IsIdentified() ? -1 : item.GetEnchantBonus());
    }

    public static void WriteInfo(ByteBuffer buf, Item item, int optionalManastoneSockets, int enchantBonus)
    {
        WriteC(buf, item.IsSoulBound() ? 1 : 0);
        WriteC(buf, item.GetEnchantLevel()); // enchant (1-15)
        WriteD(buf, item.GetItemSkinTemplate().GetTemplateId());
        WriteC(buf, optionalManastoneSockets);
        WriteC(buf, enchantBonus);

        Dictionary<int, ManaStone> stonesBySlot = CreateManastoneMap(item);
        for (int i = 0; i < Item.MAX_BASIC_STONES; i++)
        {
            stonesBySlot.TryGetValue(i, out ManaStone stone);
            WriteD(buf, stone == null ? 0 : stone.GetItemId());
        }

        WriteD(buf, item.GetGodStoneId());

        int dyeExpiration = item.GetColorTimeLeft();
        WriteDyeInfo(buf, dyeExpiration < 0 ? (int?)null : item.GetItemColor());
        WriteC(buf, 0); // unk (0)
        WriteD(buf, 0); // unk 1.5.1.9
        WriteD(buf, Math.Max(0, dyeExpiration)); // seconds until dye expires

        IdianStone idianStone = item.GetIdianStone();
        if (idianStone != null && idianStone.GetPolishNumber() > 0)
        {
            WriteD(buf, idianStone.GetItemId()); // Idian Stone template ID
            WriteC(buf, idianStone.GetPolishNumber()); // polish statset ID
        }
        else
        {
            WriteD(buf, 0); // Idian Stone template ID
            WriteC(buf, 0); // polish statset ID
        }

        WriteC(buf, item.GetTempering()); // tempering level

        WriteD(buf, 0x00);
        WriteC(buf, 0x00);
        WriteD(buf, 0x00);
        WriteC(buf, 0x00);
        WriteD(buf, 0x00);
        WriteD(buf, 0x00);

        if (item.GetTempering() > 0 && item.GetItemTemplate().GetItemGroup() == ItemGroup.PLUME)
        {
            PlumStatEnum stat = item.GetItemTemplate().GetTemperingName().Equals("TSHIRT_PHYSICAL") ? PlumStatEnum.PLUM_PHISICAL_ATTACK
                : PlumStatEnum.PLUM_BOOST_MAGICAL_SKILL;
            WriteD(buf, PlumStatEnum.PLUM_HP.GetId()); // 1st satId
            WriteD(buf, PlumStatEnum.PLUM_HP.GetBoostValue() * item.GetTempering()); // value
            WriteD(buf, stat.GetId()); // 2nd statId
            WriteD(buf, (stat.GetBoostValue() * item.GetTempering()) + item.GetRndPlumeBonusValue()); // value
        }
        else
        {
            WriteD(buf, 0x00); // 1st statId
            WriteD(buf, 0x00); // value
            WriteD(buf, 0x00); // 2nd statId
            WriteD(buf, 0x00); // value
        }
        WriteD(buf, 0x00); // 3rd statId
        WriteD(buf, 0x00);
        WriteD(buf, 0x00); // 4th statId
        WriteD(buf, 0x00);
        WriteD(buf, 0x00); // 5th statId
        WriteD(buf, 0x00);
        WriteD(buf, 0x00); // 6th statId
        WriteD(buf, 0x00);
        WriteD(buf, 0x00); // unk 4.7.5
        WriteC(buf, item.IsAmplified() ? 1 : 0);
        WriteD(buf, item.GetBuffSkill());
        WriteD(buf, 0x00); // skillId
        WriteD(buf, 0x00); // skillId
    }

    private static Dictionary<int, ManaStone> CreateManastoneMap(Item item)
    {
        if (item.HasManaStones())
        {
            Dictionary<int, ManaStone> map = new Dictionary<int, ManaStone>();
            foreach (ManaStone s in item.GetItemStones())
                map[s.GetSlot()] = s;
            return map;
        }
        return new Dictionary<int, ManaStone>();
    }

    public override int GetSize()
    {
        return SIZE;
    }
}
