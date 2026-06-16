using System;
using Aion.GameServer.Model.Templates.Items.Enums;
using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Model.Templates.Items;

/// <summary>
/// Java parity: model/templates/item/ItemTemplate extends VisibleObjectTemplate. Ported as partials (569L).
/// Part 1: XML-bound fields, afterUnmarshal, core accessors (mask/slot/class-restrict/quality/flags).
/// </summary>
[XmlType("ItemTemplate")]
public partial class ItemTemplate : VisibleObjectTemplate
{
    // Public so XmlSerializer can populate these (JAXB read private fields via @XmlAccessorType(FIELD));
    // faithful Java field names + [Xml*] attribute/element names unchanged. itemId is set via the XmlUid proxy.
    [XmlIgnore] public int itemId;
    [XmlElement("modifiers")] public Aion.GameServer.Model.Templates.Stats.ModifiersTemplate modifiers;
    [XmlElement("actions")] public Aion.GameServer.Model.Templates.Items.Actions.ItemActions actions;
    [XmlAttribute("mask")] public int mask;
    [XmlAttribute("weapon_boost")] public int weaponBoost;
    [XmlAttribute("price")] public int price;
    [XmlAttribute("max_stack_count")] public int maxStackCount = 1;
    [XmlAttribute("item_group")] public Aion.GameServer.Model.Templates.Items.Enums.ItemGroup itemGroup = Aion.GameServer.Model.Templates.Items.Enums.ItemGroup.NONE;
    [XmlAttribute("pack_count")] public int packCount;
    [XmlAttribute("level")] public int level;
    [XmlAttribute("quality")] public ItemQuality itemQuality;
    [XmlAttribute("item_type")] public ItemType itemType;
    [XmlAttribute("attack_type")] public ItemAttackType attackType;
    [XmlAttribute("attack_gap")] public float attackGap;
    [XmlAttribute("desc")] public int description;
    [XmlAttribute("option_slot_bonus")] public int optionSlotBonus;
    [XmlAttribute("rnd_bonus")] public int rndBonusId = 0;
    [XmlAttribute("rnd_count")] public int maxTuneCount = -1;
    [XmlAttribute("race")] public Race race = Race.PC_ALL;
    [XmlAttribute("return_world")] public int returnWorldId;
    [XmlAttribute("return_alias")] public string returnAlias;
    [XmlElement("godstone")] public GodstoneInfo godstoneInfo;
    [XmlElement("stigma")] public Stigma stigma;
    [XmlAttribute("name")] public string name;
    [XmlIgnore] public byte[] levelRestrictions;
    [XmlIgnore] public byte[] maxLevelRestrictions;
    [XmlAttribute("m_slots")] public int manastoneSlots;
    [XmlAttribute("s_slots")] public int specialSlots;
    [XmlAttribute("max_enchant")] public int maxEnchant;
    [XmlAttribute("max_enchant_bonus")] public int maxEnchantBonus;
    [XmlAttribute("enchant_type")] public int enchantType;
    [XmlAttribute("max_tampering")] public int maxTampering;
    [XmlAttribute("temp_exchange_time")] public int temExchangeTime;
    [XmlAttribute("expire_time")] public int expireTime;
    [XmlElement("weapon_stats")] public WeaponStats weaponStats;
    [XmlAttribute("activate_target")] public ItemActivationTarget activationTarget;
    [XmlAttribute("tempering_name")] public string temperingName;
    [XmlAttribute("enchant_name")] public string enchantName;
    [XmlAttribute("activate_count")] public int activationCount;
    [XmlAttribute("activate_combat")] public bool activateCombat;
    [XmlIgnore] public int? robotId;
    [XmlElement("tradein_list")] public TradeinList tradeinList;
    [XmlElement("acquisition")] public Acquisition acquisition;
    [XmlElement("disposition")] public Disposition disposition;
    [XmlElement("improve")] public Improvement improvement;
    [XmlElement("uselimits")] public ItemUseLimits useLimits;
    [XmlElement("inventory")] public ExtraInventory extraInventory;
    [XmlElement("idian")] public Idian idianAction;
    [XmlAttribute("can_exceed_enchant")] public bool canExceedEnchant;
    [XmlAttribute("exceed_enchant_skill")] public ExceedEnchantSkillSetType exceedEnchantSkill;

    private static readonly WeaponStats emptyWeaponStats = new WeaponStats();
    private static readonly ItemUseLimits emptyUseLimits = new ItemUseLimits();

    // Java parity: @XmlID setXmlUid(String) — item id arrives as the "id" attribute string.
    [XmlAttribute("id")]
    public string XmlUid
    {
        get => itemId.ToString();
        set => itemId = int.Parse(value);
    }

    // Java parity: @XmlAttribute("robot") Integer (nullable). XmlSerializer cannot bind a nullable value-type
    // attribute directly, so round-trip via a string proxy (null when absent → GetRobotId returns 0).
    [XmlAttribute("robot")]
    public string RobotRaw
    {
        get => robotId?.ToString();
        set => robotId = value == null ? (int?)null : int.Parse(value);
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
    // XmlSerializer does not fire JAXB unmarshal callbacks, so ItemData.AfterUnmarshal calls this per template,
    // and we propagate the nested Stigma.afterUnmarshal callback here (JAXB would fire it independently).
    public void AfterUnmarshal(object parent)
    {
        stigma?.AfterUnmarshal();

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

    public Aion.GameServer.Model.Templates.Items.Actions.ItemActions GetActions()
    {
        return actions;
    }

    public Aion.GameServer.Model.Templates.Items.Enums.ItemSubType GetItemSubType()
    {
        return itemGroup.GetItemSubType();
    }

    public Aion.GameServer.Model.Templates.Items.Enums.EquipType GetEquipmentType()
    {
        return itemGroup.GetEquipType();
    }

    // GetItemGroup/GetTemperingName/GetEnchantName/ModifyMask are defined in ItemTemplate.Part2.cs.

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
        return GetEquipmentType() == Aion.GameServer.Model.Templates.Items.Enums.EquipType.WEAPON;
    }

    public bool IsArmor()
    {
        return GetEquipmentType() == Aion.GameServer.Model.Templates.Items.Enums.EquipType.ARMOR;
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
        return GetItemSubType() == Aion.GameServer.Model.Templates.Items.Enums.ItemSubType.TWO_HAND;
    }

    public bool IsOneHandWeapon()
    {
        if (!IsWeapon())
            return false;
        return GetItemSubType() == Aion.GameServer.Model.Templates.Items.Enums.ItemSubType.ONE_HAND;
    }

    public int GetTempExchangeTime()
    {
        return temExchangeTime;
    }
}
