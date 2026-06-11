using System;
using System.Collections.Generic;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>
/// Java parity: model/gameobjects/Item extends AionObject implements Expirable, StatOwner, Persistable.
/// Ported as partial classes (959L). Part 1: fields, constructors, charge-info, core accessors.
/// </summary>
public partial class Item : AionObject, Aion.GameServer.Model.IExpirable, Aion.GameServer.Model.Stats.Calc.IStatOwner, IPersistable
{
    public const int MAX_BASIC_STONES = 6;
    private long itemCount = 1;
    private int? itemColor;
    private int colorExpireTime = 0;
    private string itemCreator;
    private readonly Aion.GameServer.Model.Templates.Items.ItemTemplate itemTemplate;
    private Aion.GameServer.Model.Templates.Items.ItemTemplate itemSkinTemplate;
    private Aion.GameServer.Model.Templates.Items.ItemTemplate fusionedItemTemplate;
    private bool isEquipped = false;
    private long equipmentSlot = Aion.GameServer.Model.Items.Storage.ItemStorage.FIRST_AVAILABLE_SLOT;
    private IPersistable.PersistentState persistentState;
    private ISet<Aion.GameServer.Model.Items.ManaStone> manaStones;
    private ISet<Aion.GameServer.Model.Items.ManaStone> fusionStones;
    private int optionalSockets;
    private int fusionedItemOptionalSockets;
    private Aion.GameServer.Model.Items.GodStone godStone;
    private Aion.GameServer.Model.Items.IdianStone idianStone;
    private bool isSoulBound = false;
    private int itemLocation;
    private int enchantLevel;
    private int enchantBonus;
    private int expireTime = 0;
    private int temporaryExchangeTime = 0;
    private long repurchasePrice;
    private int activationCount = 0;
    private Aion.GameServer.Model.Items.ChargeInfo conditioningInfo;
    private List<Aion.GameServer.Model.Stats.Calc.Functions.StatFunction> currentModifiers;
    private int tuneCount = 0;
    private Aion.GameServer.Model.Items.RandomBonusEffect bonusStatsEffect;
    private Aion.GameServer.Model.Items.RandomBonusEffect fusionedItemBonusStatsEffect;
    private int packCount;
    private int tempering;
    private Aion.GameServer.Model.Enchants.EnchantEffect enchantEffect;
    private Aion.GameServer.Model.Enchants.TemperingEffect temperingEffect;
    private bool isAmplified = false;
    private int buffSkill;
    private int rndPlumeBonusValue;
    private Aion.GameServer.Model.Items.PendingTuneResult pendingTuneResult;

    /// <summary>Create simple item with minimum information</summary>
    public Item(int objId, Aion.GameServer.Model.Templates.Items.ItemTemplate itemTemplate)
        : base(objId)
    {
        this.itemTemplate = itemTemplate;
        this.activationCount = itemTemplate.GetActivationCount();
        if (itemTemplate.GetExpireTime() != 0)
            expireTime = ((int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000) + itemTemplate.GetExpireTime() * 60) - 1;
        if (itemTemplate.CanTune())
            tuneCount = -1; // not identified yet (bonus stats need to be rolled)
        isAmplified = itemTemplate.GetEnchantType() == 1;
        this.persistentState = IPersistable.PersistentState.NEW;
        UpdateChargeInfo(0);
    }

    /// <summary>This constructor should be called from ItemService for newly created items and loadedFromDb</summary>
    public Item(int objId, Aion.GameServer.Model.Templates.Items.ItemTemplate itemTemplate, long itemCount, bool isEquipped, long equipmentSlot)
        : this(objId, itemTemplate)
    {
        this.itemCount = itemCount;
        this.isEquipped = isEquipped;
        this.equipmentSlot = equipmentSlot;
    }

    /// <summary>This constructor should be called only from DAO while loading from DB</summary>
    public Item(int objId, int itemId, long itemCount, int? itemColor, int colorExpires, string itemCreator, int expireTime, int activationCount,
        bool isEquipped, bool isSoulBound, long equipmentSlot, int itemLocation, int enchant, int enchantBonus, int itemSkin, int fusionedItem,
        int optionalSockets, int fusionedItemOptionalSockets, int charge, int tuneCount, int statBonusId, int fusionedItemStatBonusId, int tempering,
        int packCount, bool isAmplified, int buffSkill, int rndPlumeBonusValue)
        : base(objId)
    {
        this.itemTemplate = DataManager.ITEM_DATA.GetItemTemplate(itemId) ?? throw new ArgumentNullException(nameof(itemId), "Missing template for item " + itemId);
        this.itemCount = itemCount;
        this.itemColor = itemColor;
        this.colorExpireTime = colorExpires;
        this.itemCreator = itemCreator;
        this.expireTime = expireTime;
        this.activationCount = activationCount;
        this.isEquipped = isEquipped;
        this.isSoulBound = isSoulBound;
        this.equipmentSlot = equipmentSlot;
        this.itemLocation = itemLocation;
        this.enchantLevel = enchant;
        this.enchantBonus = enchantBonus;
        this.fusionedItemTemplate = DataManager.ITEM_DATA.GetItemTemplate(fusionedItem);
        this.itemSkinTemplate = DataManager.ITEM_DATA.GetItemTemplate(itemSkin);
        this.tuneCount = tuneCount;
        this.optionalSockets = optionalSockets;
        this.fusionedItemOptionalSockets = fusionedItemOptionalSockets;
        if (tuneCount == -1 && !itemTemplate.CanTune())
        {
            this.tuneCount = 0; // NC made it not tunable
        }
        this.tempering = tempering;
        this.packCount = packCount;
        this.isAmplified = isAmplified;
        this.buffSkill = buffSkill;
        this.rndPlumeBonusValue = rndPlumeBonusValue;
        if (itemTemplate.GetStatBonusSetId() != 0 && statBonusId > 0)
        {
            SetBonusStats(statBonusId, false);
        }
        if (fusionedItemTemplate != null)
        {
            if (fusionedItemTemplate.GetStatBonusSetId() != 0 && fusionedItemStatBonusId > 0)
            {
                SetFusionedItemBonusStats(fusionedItemStatBonusId, false);
            }
            if (!itemTemplate.IsCanFuse() || !itemTemplate.IsTwoHandWeapon() || !fusionedItemTemplate.IsCanFuse()
                || !fusionedItemTemplate.IsTwoHandWeapon())
            {
                this.fusionedItemTemplate = null;
                this.fusionedItemOptionalSockets = 0;
            }
        }
        UpdateChargeInfo(charge);
    }

    public int GetTempering()
    {
        return tempering;
    }

    public void SetTempering(int tempering)
    {
        this.tempering = tempering;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    private void UpdateChargeInfo(int charge)
    {
        int chargeLevel = CalculateMaxChargeLevel();
        if (conditioningInfo == null && chargeLevel > 0)
            conditioningInfo = new Aion.GameServer.Model.Items.ChargeInfo(charge, this);
        // when break fusioned item and second item has conditioned info - set to null
        if (conditioningInfo != null && chargeLevel == 0)
            conditioningInfo = null;
    }

    public override string Name => itemTemplate.GetName();

    /// <summary>itemCreator</summary>
    public string GetItemCreator()
    {
        return itemCreator == null ? "" : itemCreator;
    }

    /// <param name="itemCreator">the itemCreator to set</param>
    public void SetItemCreator(string itemCreator)
    {
        this.itemCreator = itemCreator;
    }

    public string GetItemName()
    {
        return itemTemplate.GetName();
    }

    public int GetOptionalSockets()
    {
        return optionalSockets;
    }

    public void SetOptionalSockets(int optionalSockets)
    {
        this.optionalSockets = optionalSockets;
    }

    public bool HasOptionalSocket()
    {
        return optionalSockets != 0;
    }

    public int GetFusionedItemOptionalSockets()
    {
        return fusionedItemOptionalSockets;
    }

    public bool HasOptionalFusionSocket()
    {
        return fusionedItemOptionalSockets != 0;
    }

    public void SetFusionedItemOptionalSockets(int fusionedItemOptionalSockets)
    {
        this.fusionedItemOptionalSockets = fusionedItemOptionalSockets;
    }

    public int GetEnchantBonus()
    {
        return enchantBonus;
    }

    public void SetEnchantBonus(int enchantBonus)
    {
        this.enchantBonus = enchantBonus;
    }

    public bool IsStigmaChargeable()
    {
        return itemTemplate.GetStigma() != null && itemTemplate.GetStigma().IsChargeable();
    }

    /// <summary>the itemTemplate</summary>
    public Aion.GameServer.Model.Templates.Items.ItemTemplate GetItemTemplate()
    {
        return itemTemplate;
    }

    /// <summary>the itemAppearanceTemplate</summary>
    public Aion.GameServer.Model.Templates.Items.ItemTemplate GetItemSkinTemplate()
    {
        if (this.itemSkinTemplate == null)
            return this.itemTemplate;
        return this.itemSkinTemplate;
    }

    public void SetItemSkinTemplate(Aion.GameServer.Model.Templates.Items.ItemTemplate newTemplate)
    {
        this.itemSkinTemplate = newTemplate;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    public bool IsSkinnedItem()
    {
        return GetItemSkinTemplate() != this.itemTemplate;
    }

    /// <summary>RGB color value (no alpha channel info) or null if not dyed</summary>
    public int? GetItemColor()
    {
        return itemColor;
    }

    /// <param name="color">the item color to set (RGB color value or null to remove dye)</param>
    public void SetItemColor(int? color)
    {
        this.itemColor = color == null ? (int?)null : color & 0xFFFFFF; // bit mask ensures valid value range (no alpha channel support)
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    /// <summary>positive values if not expired, 0 for not expirable, negative for expired</summary>
    public int GetColorTimeLeft()
    {
        if (colorExpireTime == 0)
            return 0;
        return (int)(colorExpireTime - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000);
    }

    public int GetColorExpireTime()
    {
        return colorExpireTime;
    }

    public void SetColorExpireTime(int dyeRemainsUntil)
    {
        this.colorExpireTime = dyeRemainsUntil;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    /// <summary>the itemCount — number of this item in stack.</summary>
    public long GetItemCount()
    {
        return itemCount;
    }

    public long GetFreeCount()
    {
        return itemTemplate.GetMaxStackCount() - itemCount;
    }

    /// <param name="itemCount">the itemCount to set</param>
    public void SetItemCount(long itemCount)
    {
        this.itemCount = itemCount;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }
}
