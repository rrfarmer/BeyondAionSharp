using System;
using System.Collections.Generic;
using Aion.Commons.Nio;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;

namespace Aion.GameServer.Network.Aion.Iteminfo;

/// <summary>
/// Java parity: network/aion/iteminfo/ItemInfoBlob (-Nemesiss-, Rolandas). Entry item info packet data (contains blob entries with
/// detailed info). Per-constant abstract-method enum ItemBlobType -> abstract nested class + sealed singleton instances (reference
/// equality via ==); enum.name() -> Name property. ItemTemplate/ArmorType/ItemGroup/StatFunction red-tolerated.
/// </summary>
public class ItemInfoBlob : PacketWriteHelper
{
    private readonly Player player;
    private readonly Item item;
    private readonly List<ItemBlobEntry> itemBlobEntries = new List<ItemBlobEntry>();

    public ItemInfoBlob(Player player, Item item)
    {
        this.player = player;
        this.item = item;
    }

    protected override void WriteMe(ByteBuffer buf)
    {
        WriteH(buf, Size());
        foreach (ItemBlobEntry ent in itemBlobEntries)
            ent.WriteMeInternal(buf);
    }

    public void AddBlobEntry(ItemBlobType type)
    {
        ItemBlobEntry ent = type.NewBlobEntry();
        ent.SetOwner(player, item, null);
        itemBlobEntries.Add(ent);
    }

    public void AddBonusBlobEntry(IStatFunction modifier)
    {
        ItemBlobEntry ent = ItemBlobType.STAT_BONUSES.NewBlobEntry();
        ent.SetOwner(player, item, modifier);
        itemBlobEntries.Add(ent);
    }

    public static ItemBlobEntry NewBlobEntry(ItemBlobType type, Player player, Item item)
    {
        if (type == ItemBlobType.STAT_BONUSES)
            throw new NotSupportedException();
        ItemBlobEntry ent = type.NewBlobEntry();
        ent.SetOwner(player, item, null);
        return ent;
    }

    public static ItemInfoBlob GetFullBlob(Player player, Item item)
    {
        ItemInfoBlob blob = new ItemInfoBlob(player, item);

        ItemTemplate itemTemplate = item.GetItemTemplate();

        if (item.HasFusionedItem() || itemTemplate.IsTwoHandWeapon())
            blob.AddBlobEntry(ItemBlobType.COMPOSITE_ITEM);

        if (itemTemplate.GetItemGroup().GetValidEquipmentSlots() != 0)
        {
            // EQUIPPED SLOT
            blob.AddBlobEntry(ItemBlobType.EQUIPPED_SLOT);

            if (itemTemplate.GetItemGroup() == ItemGroup.WING)
            {
                blob.AddBlobEntry(ItemBlobType.SLOTS_WING);
            }
            else if (itemTemplate.GetItemGroup() == ItemGroup.SHIELD)
            {
                blob.AddBlobEntry(ItemBlobType.SLOTS_SHIELD);
            }
            else if (itemTemplate.GetItemGroup() == ItemGroup.PLUME)
            {
                blob.AddBlobEntry(ItemBlobType.PLUME_INFO);
            }
            else if (itemTemplate.IsArmor())
            {
                if (itemTemplate.GetItemGroup().GetArmorType() == ArmorType.ACCESSORY)
                    blob.AddBlobEntry(ItemBlobType.SLOTS_ACCESSORY); // power shards, helmets, earrings, rings, belts
                else
                    blob.AddBlobEntry(ItemBlobType.SLOTS_ARMOR);
            }
            else if (itemTemplate.IsWeapon())
            {
                blob.AddBlobEntry(ItemBlobType.SLOTS_WEAPON);
            }

            blob.AddBlobEntry(ItemBlobType.ENCHANT_INFO);

            if (item.GetConditioningInfo() != null)
                blob.AddBlobEntry(ItemBlobType.CONDITIONING_INFO);

            // All items with only General
            if (blob.GetBlobEntries().Count > 0)
            {
                if (itemTemplate.IsCanPolish())
                    blob.AddBlobEntry(ItemBlobType.POLISH_INFO);
                blob.AddBlobEntry(ItemBlobType.PREMIUM_OPTION);
            }

            List<StatFunction> allModifiers = itemTemplate.GetModifiers();
            if (allModifiers != null)
            {
                foreach (IStatFunction modifier in allModifiers)
                {
                    if (modifier.IsBonus() && !modifier.HasConditions())
                    {
                        blob.AddBonusBlobEntry(modifier);
                    }
                }
            }
        }

        if (itemTemplate.GetItemGroup() == ItemGroup.STIGMA_SHARD)
            blob.AddBlobEntry(ItemBlobType.STIGMA_SHARD);

        // GENERAL INFO
        blob.AddBlobEntry(ItemBlobType.GENERAL_INFO);
        if (item.GetPackCount() != 0)
        {
            blob.AddBlobEntry(ItemBlobType.WRAP_INFO);
        }
        return blob;
    }

    public List<ItemBlobEntry> GetBlobEntries()
    {
        return itemBlobEntries;
    }

    public List<ItemBlobEntryMetadata> GetBlobEntryMetadata()
    {
        List<ItemBlobEntryMetadata> metadata = new List<ItemBlobEntryMetadata>();
        foreach (ItemBlobEntry ent in itemBlobEntries)
            metadata.Add(new ItemBlobEntryMetadata(ent.GetItemBlobType().Name, ent.GetItemBlobType().GetEntryId(), ent.GetSize()));
        return metadata;
    }

    public static List<ItemBlobEntryMetadata> GetFullBlobEntryMetadata(Player player, Item item)
    {
        return GetFullBlob(player, item).GetBlobEntryMetadata();
    }

    public int Size()
    {
        int totalSize = 0;
        foreach (ItemBlobEntry ent in itemBlobEntries)
            totalSize += ent.GetSize() + 1; // 1 C for blob id
        return totalSize;
    }

    public abstract class ItemBlobType
    {
        public static readonly ItemBlobType GENERAL_INFO = new GeneralInfoType(0x00);
        public static readonly ItemBlobType SLOTS_WEAPON = new WeaponType(0x01);
        public static readonly ItemBlobType SLOTS_ARMOR = new ArmorType_(0x02);
        public static readonly ItemBlobType SLOTS_SHIELD = new ShieldType(0x03);
        public static readonly ItemBlobType SLOTS_ACCESSORY = new AccessoryType(0x04);
        public static readonly ItemBlobType SLOTS_ARROW = new ArrowType(0x05);
        public static readonly ItemBlobType EQUIPPED_SLOT = new EquippedSlotType(0x06);
        // Removed from 3.5
        public static readonly ItemBlobType STIGMA_INFO = new StigmaInfoType(0x07);
        // Removed from 4.5, added back in 4.7
        public static readonly ItemBlobType STIGMA_SHARD = new StigmaShardType(0x08);
        // missing(0x09), //15? [Not handled before]
        public static readonly ItemBlobType PREMIUM_OPTION = new PremiumOptionType(0x10);
        public static readonly ItemBlobType POLISH_INFO = new PolishType(0x11);
        public static readonly ItemBlobType WRAP_INFO = new WrapType(0x12);
        public static readonly ItemBlobType PLUME_INFO = new PlumeType(0x13);
        public static readonly ItemBlobType STAT_BONUSES = new BonusType(0x0A);
        public static readonly ItemBlobType ENCHANT_INFO = new EnchantType(0x0B);
        // 0x0C - not used?
        public static readonly ItemBlobType SLOTS_WING = new WingType(0x0D);
        public static readonly ItemBlobType COMPOSITE_ITEM = new CompositeType(0x0E);
        public static readonly ItemBlobType CONDITIONING_INFO = new ConditioningType(0x0F);

        private readonly int entryId;
        private readonly string name;

        protected ItemBlobType(int entryId, string name)
        {
            this.entryId = entryId;
            this.name = name;
        }

        public int GetEntryId()
        {
            return entryId;
        }

        /// <summary>Java enum name() equivalent.</summary>
        public string Name => name;

        public abstract ItemBlobEntry NewBlobEntry();

        private sealed class GeneralInfoType : ItemBlobType
        {
            public GeneralInfoType(int entryId) : base(entryId, "GENERAL_INFO") { }
            public override ItemBlobEntry NewBlobEntry() => new GeneralInfoBlobEntry();
        }

        private sealed class WeaponType : ItemBlobType
        {
            public WeaponType(int entryId) : base(entryId, "SLOTS_WEAPON") { }
            public override ItemBlobEntry NewBlobEntry() => new WeaponInfoBlobEntry();
        }

        private sealed class ArmorType_ : ItemBlobType
        {
            public ArmorType_(int entryId) : base(entryId, "SLOTS_ARMOR") { }
            public override ItemBlobEntry NewBlobEntry() => new ArmorInfoBlobEntry();
        }

        private sealed class ShieldType : ItemBlobType
        {
            public ShieldType(int entryId) : base(entryId, "SLOTS_SHIELD") { }
            public override ItemBlobEntry NewBlobEntry() => new ShieldInfoBlobEntry();
        }

        private sealed class AccessoryType : ItemBlobType
        {
            public AccessoryType(int entryId) : base(entryId, "SLOTS_ACCESSORY") { }
            public override ItemBlobEntry NewBlobEntry() => new AccessoryInfoBlobEntry();
        }

        private sealed class ArrowType : ItemBlobType
        {
            public ArrowType(int entryId) : base(entryId, "SLOTS_ARROW") { }
            public override ItemBlobEntry NewBlobEntry() => new ArrowInfoBlobEntry();
        }

        private sealed class EquippedSlotType : ItemBlobType
        {
            public EquippedSlotType(int entryId) : base(entryId, "EQUIPPED_SLOT") { }
            public override ItemBlobEntry NewBlobEntry() => new EquippedSlotBlobEntry();
        }

        private sealed class StigmaInfoType : ItemBlobType
        {
            public StigmaInfoType(int entryId) : base(entryId, "STIGMA_INFO") { }
            public override ItemBlobEntry NewBlobEntry() => new StigmaInfoBlobEntry();
        }

        private sealed class StigmaShardType : ItemBlobType
        {
            public StigmaShardType(int entryId) : base(entryId, "STIGMA_SHARD") { }
            public override ItemBlobEntry NewBlobEntry() => new StigmaShardInfoBlobEntry();
        }

        private sealed class PremiumOptionType : ItemBlobType
        {
            public PremiumOptionType(int entryId) : base(entryId, "PREMIUM_OPTION") { }
            public override ItemBlobEntry NewBlobEntry() => new PremiumOptionInfoBlobEntry();
        }

        private sealed class PolishType : ItemBlobType
        {
            public PolishType(int entryId) : base(entryId, "POLISH_INFO") { }
            public override ItemBlobEntry NewBlobEntry() => new PolishInfoBlobEntry();
        }

        private sealed class WrapType : ItemBlobType
        {
            public WrapType(int entryId) : base(entryId, "WRAP_INFO") { }
            public override ItemBlobEntry NewBlobEntry() => new WrapInfoBlobEntry();
        }

        private sealed class PlumeType : ItemBlobType
        {
            public PlumeType(int entryId) : base(entryId, "PLUME_INFO") { }
            public override ItemBlobEntry NewBlobEntry() => new PlumeInfoBlobEntry();
        }

        private sealed class BonusType : ItemBlobType
        {
            public BonusType(int entryId) : base(entryId, "STAT_BONUSES") { }
            public override ItemBlobEntry NewBlobEntry() => new BonusInfoBlobEntry();
        }

        private sealed class EnchantType : ItemBlobType
        {
            public EnchantType(int entryId) : base(entryId, "ENCHANT_INFO") { }
            public override ItemBlobEntry NewBlobEntry() => new EnchantInfoBlobEntry();
        }

        private sealed class WingType : ItemBlobType
        {
            public WingType(int entryId) : base(entryId, "SLOTS_WING") { }
            public override ItemBlobEntry NewBlobEntry() => new WingInfoBlobEntry();
        }

        private sealed class CompositeType : ItemBlobType
        {
            public CompositeType(int entryId) : base(entryId, "COMPOSITE_ITEM") { }
            public override ItemBlobEntry NewBlobEntry() => new CompositeItemBlobEntry();
        }

        private sealed class ConditioningType : ItemBlobType
        {
            public ConditioningType(int entryId) : base(entryId, "CONDITIONING_INFO") { }
            public override ItemBlobEntry NewBlobEntry() => new ConditioningInfoBlobEntry();
        }
    }

    public sealed class ItemBlobEntryMetadata
    {
        private readonly string entryName;
        private readonly int entryId;
        private readonly int payloadSize;

        internal ItemBlobEntryMetadata(string entryName, int entryId, int payloadSize)
        {
            this.entryName = entryName;
            this.entryId = entryId;
            this.payloadSize = payloadSize;
        }

        public string GetEntryName()
        {
            return entryName;
        }

        public int GetEntryId()
        {
            return entryId;
        }

        public int GetPayloadSize()
        {
            return payloadSize;
        }
    }
}
