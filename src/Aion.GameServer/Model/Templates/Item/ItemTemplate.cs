using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Model.Templates.Item;

/// <summary>
/// Java parity: model/templates/item/ItemTemplate extends VisibleObjectTemplate. Ported as partials (569L).
/// Part 1: XML-bound fields, afterUnmarshal, core accessors (mask/slot/class-restrict/quality/flags).
/// </summary>
[XmlType("ItemTemplate")]
public partial class ItemTemplate : VisibleObjectTemplate
{
    private int itemId;
    [XmlElement("modifiers")] private Aion.GameServer.Model.Templates.Stats.ModifiersTemplate modifiers;
    [XmlElement("actions")] private Aion.GameServer.Model.Templates.Item.Actions.ItemActions actions;
    [XmlAttribute("mask")] private int mask;
    [XmlAttribute("weapon_boost")] private int weaponBoost;
    [XmlAttribute("price")] private int price;
    [XmlAttribute("max_stack_count")] private int maxStackCount = 1;
    [XmlAttribute("item_group")] private Aion.GameServer.Model.Templates.Item.Enums.ItemGroup itemGroup = Aion.GameServer.Model.Templates.Item.Enums.ItemGroup.NONE;
    [XmlAttribute("pack_count")] private int packCount;
    [XmlAttribute("level")] private int level;
    [XmlAttribute("quality")] private ItemQuality itemQuality;
    [XmlAttribute("item_type")] private ItemType itemType;
    [XmlAttribute("attack_type")] private ItemAttackType attackType;
    [XmlAttribute("attack_gap")] private float attackGap;
    [XmlAttribute("desc")] private int description;
    [XmlAttribute("option_slot_bonus")] private int optionSlotBonus;
    [XmlAttribute("rnd_bonus")] private int rndBonusId = 0;
    [XmlAttribute("rnd_count")] private int maxTuneCount = -1;
    [XmlAttribute("race")] private Race race = Race.PC_ALL;
    [XmlAttribute("return_world")] private int returnWorldId;
    [XmlAttribute("return_alias")] private string returnAlias;
    [XmlElement("godstone")] private GodstoneInfo godstoneInfo;
    [XmlElement("stigma")] private Stigma stigma;
    [XmlAttribute("name")] private string name;
    private byte[] levelRestrictions;
    private byte[] maxLevelRestrictions;
    [XmlAttribute("m_slots")] private int manastoneSlots;
    [XmlAttribute("s_slots")] private int specialSlots;
    [XmlAttribute("max_enchant")] private int maxEnchant;
    [XmlAttribute("max_enchant_bonus")] private int maxEnchantBonus;
    [XmlAttribute("enchant_type")] private int enchantType;
    [XmlAttribute("max_tampering")] private int maxTampering;
    [XmlAttribute("temp_exchange_time")] private int temExchangeTime;
    [XmlAttribute("expire_time")] private int expireTime;
    [XmlElement("weapon_stats")] private WeaponStats weaponStats;
    [XmlAttribute("activate_target")] private ItemActivationTarget activationTarget;
    [XmlAttribute("tempering_name")] private string temperingName;
    [XmlAttribute("enchant_name")] private string enchantName;
    [XmlAttribute("activate_count")] private int activationCount;
    [XmlAttribute("activate_combat")] private bool activateCombat;
    [XmlAttribute("robot")] private int? robotId;
    [XmlElement("tradein_list")] private TradeinList tradeinList;
    [XmlElement("acquisition")] private Acquisition acquisition;
    [XmlElement("disposition")] private Disposition disposition;
    [XmlElement("improve")] private Improvement improvement;
    [XmlElement("uselimits")] private ItemUseLimits useLimits;
    [XmlElement("inventory")] private ExtraInventory extraInventory;
    [XmlElement("idian")] private Idian idianAction;
    [XmlAttribute("can_exceed_enchant")] private bool canExceedEnchant;
    [XmlAttribute("exceed_enchant_skill")] private ExceedEnchantSkillSetType exceedEnchantSkill;

    private static readonly WeaponStats emptyWeaponStats = new WeaponStats();
    private static readonly ItemUseLimits emptyUseLimits = new ItemUseLimits();

    // Java parity: @XmlID setXmlUid(String) — item id arrives as the "id" attribute string.
    [XmlAttribute("id")]
    public string XmlUid
    {
        get => itemId.ToString();
        set => itemId = int.Parse(value);
    }

    // Java parity: @XmlJavaTypeAdapter(SpaceSeparatedBytesAdapter) on "restrict".
    [XmlAttribute("restrict")]
    public string RestrictXml
    {
        get => levelRestrictions == null ? null : string.Join(" ", levelRestrictions);
        set => levelRestrictions = ParseBytes(value);
    }

    // Java parity: @XmlJavaTypeAdapter(SpaceSeparatedBytesAdapter) on "restrict_max".
    [XmlAttribute("restrict_max")]
    public string RestrictMaxXml
    {
        get => maxLevelRestrictions == null ? null : string.Join(" ", maxLevelRestrictions);
        set => maxLevelRestrictions = ParseBytes(value);
    }

    private static byte[] ParseBytes(string value)
    {
        if (value == null)
            return null;
        string[] parts = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        byte[] result = new byte[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            result[i] = byte.Parse(parts[i]);
        return result;
    }

    // Java parity: afterUnmarshal(Unmarshaller, Object) — invoked by the loader after deserialization.
    public void AfterUnmarshal()
    {
        if (weaponStats == null)
            weaponStats = emptyWeaponStats;
        if (useLimits == null)
            useLimits = emptyUseLimits;

        // check if it can be randomized
        if (GetItemSlot() == 0)
            maxTuneCount = 0;
        else if (maxTuneCount == -1)
        {
            if (maxEnchantBonus == 0 && optionSlotBonus == 0 && rndBonusId == 0)
                maxTuneCount = 0;
        }
    }

    public int GetMask()
    {
        return mask;
    }

    public long GetItemSlot()
    {
        return itemGroup.GetValidEquipmentSlots();
    }

    public bool IsClassSpecific(PlayerClass playerClass)
    {
        bool related = levelRestrictions[playerClass.GetClassId()] > 0;
        if (!related && !playerClass.IsStartingClass())
        {
            related = levelRestrictions[playerClass.GetStartingClass().GetClassId()] > 0;
        }
        return related;
    }

    public int GetRequiredLevel(PlayerClass playerClass)
    {
        int requiredLevel = levelRestrictions[playerClass.GetClassId()];
        if (requiredLevel == 0)
            return -1;
        else
            return requiredLevel;
    }

    public byte GetMaxLevelRestrict(PlayerClass playerClass)
    {
        if (maxLevelRestrictions != null)
        {
            return maxLevelRestrictions[playerClass.GetClassId()];
        }
        return 0;
    }

    public List<Aion.GameServer.Model.Stats.Calc.Functions.StatFunction> GetModifiers()
    {
        if (modifiers != null)
        {
            return modifiers.GetModifiers();
        }
        return null;
    }

    public Aion.GameServer.Model.Templates.Item.Actions.ItemActions GetActions()
    {
        return actions;
    }

    public Aion.GameServer.Model.Templates.Item.Enums.ItemSubType GetItemSubType()
    {
        return itemGroup.GetItemSubType();
    }

    public Aion.GameServer.Model.Templates.Item.Enums.EquipType GetEquipmentType()
    {
        return itemGroup.GetEquipType();
    }

    public long GetPrice()
    {
        return price;
    }

    public int GetLevel()
    {
        return level;
    }

    public ItemQuality GetItemQuality()
    {
        return itemQuality;
    }

    public ItemType GetItemType()
    {
        return itemType;
    }

    public override int GetL10nId()
    {
        return description;
    }

    public long GetMaxStackCount()
    {
        if (IsKinah())
        {
            if (Aion.GameServer.Configs.Main.CustomConfig.ENABLE_KINAH_CAP)
            {
                return Aion.GameServer.Configs.Main.CustomConfig.KINAH_CAP_VALUE;
            }
            else
            {
                return long.MaxValue;
            }
        }
        return maxStackCount;
    }

    public ItemAttackType GetAttackType()
    {
        return attackType;
    }

    public float GetAttackGap()
    {
        return attackGap;
    }

    public int GetOptionSlotBonus()
    {
        return optionSlotBonus;
    }

    public bool IsNoEnchant()
    {
        return (GetMask() & Aion.GameServer.Model.Items.ItemMask.NO_ENCHANT) == Aion.GameServer.Model.Items.ItemMask.NO_ENCHANT;
    }

    public bool IsItemDyePermitted()
    {
        return (GetMask() & Aion.GameServer.Model.Items.ItemMask.DYEABLE) == Aion.GameServer.Model.Items.ItemMask.DYEABLE;
    }

    public Race GetRace()
    {
        return race;
    }

    public int GetWeaponBoost()
    {
        return weaponBoost;
    }

    public bool IsWeapon()
    {
        return GetEquipmentType() == Aion.GameServer.Model.Templates.Item.Enums.EquipType.WEAPON;
    }

    public bool IsArmor()
    {
        return GetEquipmentType() == Aion.GameServer.Model.Templates.Item.Enums.EquipType.ARMOR;
    }

    public bool IsKinah()
    {
        return itemId == Aion.GameServer.Model.Items.ItemId.KINAH;
    }

    public bool IsStigma()
    {
        return stigma != null;
    }

    /// <summary>The associated ItemSetTemplate or null if none.</summary>
    public Aion.GameServer.Model.Templates.Itemset.ItemSetTemplate GetItemSet()
    {
        return Aion.GameServer.Dataholders.DataManager.ITEM_SET_DATA.GetItemSetTemplateByItemId(itemId);
    }

    /// <summary>Checks if the ItemTemplate belongs to an item set.</summary>
    public bool IsItemSet()
    {
        return GetItemSet() != null;
    }

    public GodstoneInfo GetGodstoneInfo()
    {
        return godstoneInfo;
    }

    public override string GetName()
    {
        return name == null ? "" : name;
    }

    public override int GetTemplateId()
    {
        return itemId;
    }

    public int GetReturnWorldId()
    {
        return returnWorldId;
    }

    public string GetReturnAlias()
    {
        return returnAlias;
    }

    public Stigma GetStigma()
    {
        return stigma;
    }

    public int GetManastoneSlots()
    {
        return manastoneSlots;
    }

    public int GetSpecialSlots()
    {
        return specialSlots;
    }

    public int GetMaxEnchantLevel()
    {
        return maxEnchant;
    }

    public int GetMaxEnchantBonus()
    {
        return maxEnchantBonus;
    }

    public bool HasLimitOne()
    {
        return (GetMask() & Aion.GameServer.Model.Items.ItemMask.LIMIT_ONE) == Aion.GameServer.Model.Items.ItemMask.LIMIT_ONE;
    }

    public bool IsTradeable()
    {
        return (GetMask() & Aion.GameServer.Model.Items.ItemMask.TRADEABLE) == Aion.GameServer.Model.Items.ItemMask.TRADEABLE;
    }

    public bool IsCanFuse()
    {
        return (GetMask() & Aion.GameServer.Model.Items.ItemMask.CAN_COMPOSITE_WEAPON) == Aion.GameServer.Model.Items.ItemMask.CAN_COMPOSITE_WEAPON;
    }

    public bool CanSplit()
    {
        return (GetMask() & Aion.GameServer.Model.Items.ItemMask.CAN_SPLIT) == Aion.GameServer.Model.Items.ItemMask.CAN_SPLIT;
    }

    public bool IsSoulBound()
    {
        return (GetMask() & Aion.GameServer.Model.Items.ItemMask.SOUL_BOUND) == Aion.GameServer.Model.Items.ItemMask.SOUL_BOUND;
    }

    public bool IsBreakable()
    {
        return (GetMask() & Aion.GameServer.Model.Items.ItemMask.BREAKABLE) == Aion.GameServer.Model.Items.ItemMask.BREAKABLE;
    }

    public bool IsDeletable()
    {
        return (GetMask() & Aion.GameServer.Model.Items.ItemMask.DELETABLE) == Aion.GameServer.Model.Items.ItemMask.DELETABLE;
    }

    public bool IsCanPolish()
    {
        return (GetMask() & Aion.GameServer.Model.Items.ItemMask.CAN_POLISH) == Aion.GameServer.Model.Items.ItemMask.CAN_POLISH;
    }

    public bool IsTwoHandWeapon()
    {
        if (!IsWeapon())
            return false;
        return GetItemSubType() == Aion.GameServer.Model.Templates.Item.Enums.ItemSubType.TWO_HAND;
    }

    public bool IsOneHandWeapon()
    {
        if (!IsWeapon())
            return false;
        return GetItemSubType() == Aion.GameServer.Model.Templates.Item.Enums.ItemSubType.ONE_HAND;
    }

    public int GetTempExchangeTime()
    {
        return temExchangeTime;
    }
}
