using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>
/// Java parity: model/gameobjects/Item — partial #2 (Java ~314-607): count math, equip slot,
/// mana/fusion stones, godstone, enchant level, Persistable impl, item location/mask/soulbound, fusion.
/// </summary>
public partial class Item
{
    private static readonly ILogger log = NullLogger.Instance;

    /// <summary>This method should be called ONLY from Storage class.</summary>
    public long IncreaseItemCount(long count)
    {
        if (count <= 0)
        {
            return 0;
        }
        long cap = itemTemplate.GetMaxStackCount();
        long addCount = this.itemCount + count > cap ? cap - this.itemCount : count;
        if (addCount != 0)
        {
            this.itemCount += addCount;
            SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        }
        return count - addCount;
    }

    /// <summary>This method should be called ONLY from Storage class.</summary>
    public long DecreaseItemCount(long count)
    {
        if (count <= 0)
        {
            return 0;
        }
        long removeCount = count >= itemCount ? itemCount : count;
        this.itemCount -= removeCount;
        if (itemCount == 0 && !this.itemTemplate.IsKinah())
        {
            SetPersistentState(IPersistable.PersistentState.DELETED);
        }
        else
        {
            SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        }
        return count - removeCount;
    }

    /// <summary>the isEquipped</summary>
    public bool IsEquipped()
    {
        return isEquipped;
    }

    /// <param name="isEquipped">the isEquipped to set</param>
    public void SetEquipped(bool isEquipped)
    {
        this.isEquipped = isEquipped;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    /// <summary>
    /// the equipmentSlot. Equipment slot can be of 2 types - ItemSlot enum type if equipped, or position in cube.
    /// </summary>
    public long GetEquipmentSlot()
    {
        return equipmentSlot;
    }

    /// <param name="equipmentSlot">the equipmentSlot to set</param>
    public void SetEquipmentSlot(long equipmentSlot)
    {
        this.equipmentSlot = equipmentSlot;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    /// <summary>Lazy initialize empty manastone list.</summary>
    public ISet<Aion.GameServer.Model.Items.ManaStone> GetItemStones()
    {
        if (manaStones == null)
            this.manaStones = ItemStonesCollection();
        return manaStones;
    }

    /// <summary>Lazy initialize empty manastone list.</summary>
    public ISet<Aion.GameServer.Model.Items.ManaStone> GetFusionStones()
    {
        if (fusionStones == null)
            this.fusionStones = ItemStonesCollection();
        return fusionStones;
    }

    public int GetFusionStonesSize()
    {
        if (fusionStones == null)
            return 0;
        return fusionStones.Count;
    }

    public int GetItemStonesSize()
    {
        if (manaStones == null)
            return 0;
        return manaStones.Count;
    }

    private ISet<Aion.GameServer.Model.Items.ManaStone> ItemStonesCollection()
    {
        // Java parity: TreeSet ordered by manastone slot.
        return new SortedSet<Aion.GameServer.Model.Items.ManaStone>(Comparer<Aion.GameServer.Model.Items.ManaStone>.Create((o1, o2) =>
        {
            if (o1.GetSlot() == o2.GetSlot())
                return 0;
            return o1.GetSlot() > o2.GetSlot() ? 1 : -1;
        }));
    }

    /// <summary>Check manastones without initialization.</summary>
    public bool HasManaStones()
    {
        return manaStones != null && manaStones.Count > 0;
    }

    /// <summary>Check fusionstones without initialization.</summary>
    public bool HasFusionStones()
    {
        return fusionStones != null && fusionStones.Count > 0;
    }

    public bool HasIdianStone()
    {
        return idianStone != null;
    }

    public bool HasGodStone()
    {
        return godStone != null;
    }

    public Aion.GameServer.Model.Items.GodStone GetGodStone()
    {
        return godStone;
    }

    public int GetGodStoneId()
    {
        return godStone == null ? 0 : godStone.GetItemId();
    }

    public void AddGodStone(int itemId)
    {
        AddGodStone(itemId, 0);
    }

    public void AddGodStone(int itemId, int activatedCount)
    {
        Aion.GameServer.Model.Templates.Items.GodstoneInfo godstoneInfo = DataManager.ITEM_DATA.GetItemTemplate(itemId).GetGodstoneInfo();
        if (godstoneInfo == null)
        {
            log.LogWarning("Item " + itemId + " has no godstone info");
            return;
        }
        if (godStone != null)
            SetGodStone(null);
        godStone = new Aion.GameServer.Model.Items.GodStone(this, activatedCount, itemId, godstoneInfo, IPersistable.PersistentState.NEW);
    }

    public void SetGodStone(Aion.GameServer.Model.Items.GodStone godStone)
    {
        if (godStone == null)
        {
            this.godStone.SetPersistentState(IPersistable.PersistentState.DELETED);
            Aion.GameServer.Dao.ItemStoneListDAO.StoreGodStones(this.godStone);
        }
        this.godStone = godStone;
    }

    /// <summary>the enchantLevel</summary>
    public int GetEnchantLevel()
    {
        return enchantLevel;
    }

    /// <param name="enchantLevel">the enchantLevel to set</param>
    public void SetEnchantLevel(int enchantLevel)
    {
        this.enchantLevel = enchantLevel;
        if (enchantLevel > 0)
            RemoveRemainingTuningCountIfPossible();
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    /// <summary>the persistentState</summary>
    public IPersistable.PersistentState GetPersistentState()
    {
        return persistentState;
    }

    /// <summary>
    /// Possible changes: NEW -&gt; UPDATED, NEW -&gt; UPDATE_REQUIRED, UPDATE_REQUIRED -&gt; DELETED,
    /// UPDATE_REQUIRED -&gt; UPDATED, UPDATED -&gt; DELETED, UPDATED -&gt; UPDATE_REQUIRED.
    /// </summary>
    public void SetPersistentState(IPersistable.PersistentState persistentState)
    {
        switch (persistentState)
        {
            case IPersistable.PersistentState.DELETED:
                if (this.persistentState == IPersistable.PersistentState.NEW)
                    this.persistentState = IPersistable.PersistentState.NOACTION;
                else
                    this.persistentState = IPersistable.PersistentState.DELETED;
                break;
            case IPersistable.PersistentState.UPDATE_REQUIRED:
                if (this.persistentState == IPersistable.PersistentState.NEW)
                    break;
                goto default;
            default:
                this.persistentState = persistentState;
                break;
        }
    }

    public void SetItemLocation(int storageType)
    {
        this.itemLocation = storageType;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    public int GetItemLocation()
    {
        return itemLocation;
    }

    public int GetItemMask()
    {
        return itemTemplate.GetMask();
    }

    public bool IsSoulBound()
    {
        return isSoulBound;
    }

    public void SetSoulBound(bool isSoulBound)
    {
        this.isSoulBound = isSoulBound;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    public Aion.GameServer.Model.Templates.Items.Enums.EquipType GetEquipmentType()
    {
        if (itemTemplate.IsStigma())
            return Aion.GameServer.Model.Templates.Items.Enums.EquipType.STIGMA;
        return itemTemplate.GetEquipmentType();
    }

    public int GetItemId()
    {
        return itemTemplate.GetTemplateId();
    }

    public string GetL10n()
    {
        return itemTemplate.GetL10n();
    }

    public bool HasFusionedItem()
    {
        return fusionedItemTemplate != null;
    }

    public Aion.GameServer.Model.Templates.Items.ItemTemplate GetFusionedItemTemplate()
    {
        return fusionedItemTemplate;
    }

    public int GetFusionedItemId()
    {
        return fusionedItemTemplate != null ? fusionedItemTemplate.GetTemplateId() : 0;
    }

    public void SetFusionedItem(Item fusionedItem)
    {
        if (fusionedItem == null)
            SetFusionedItem(null, 0, 0);
        else
            SetFusionedItem(fusionedItem.GetItemTemplate(), fusionedItem.GetBonusStatsId(), fusionedItem.GetOptionalSockets());
    }

    public void SetFusionedItem(Aion.GameServer.Model.Templates.Items.ItemTemplate template, int bonusStatsId, int optionalSockets)
    {
        RemoveAllFusionStones();
        fusionedItemTemplate = template;
        SetFusionedItemBonusStats(bonusStatsId, false);
        SetFusionedItemOptionalSockets(optionalSockets);
        UpdateChargeInfo(0);
        if (template != null)
            RemoveRemainingTuningCountIfPossible();
    }

    private void RemoveAllFusionStones()
    {
        if (!HasFusionStones())
            return;
        foreach (Aion.GameServer.Model.Items.ManaStone ms in fusionStones)
            ms.SetPersistentState(IPersistable.PersistentState.DELETED);
        Aion.GameServer.Dao.ItemStoneListDAO.StoreFusionStone(fusionStones);
        fusionStones.Clear();
    }
}
