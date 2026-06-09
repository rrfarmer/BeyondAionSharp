using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>
/// Java parity: model/gameobjects/Item — partial #3 (Java ~609-958): sockets, storable/trade masks,
/// expire/Expirable, charge/conditioning, improvement, StatOwner modifiers, idian, bonus-stats, tune,
/// enchant/temper effects, pack, enchant params, amplify, buff-skill, plume, pending-tune, toString.
/// </summary>
public partial class Item
{
    public int GetSockets(bool isFusionItem)
    {
        int numSockets;
        if (itemTemplate.IsWeapon() || itemTemplate.IsArmor())
        {
            if (isFusionItem)
            {
                Aion.GameServer.Model.Templates.Item.ItemTemplate fusedTemp = GetFusionedItemTemplate();
                if (fusedTemp == null)
                    return 0;
                numSockets = fusedTemp.GetManastoneSlots() + GetFusionedItemOptionalSockets();
            }
            else
            {
                numSockets = GetItemTemplate().GetManastoneSlots() + GetOptionalSockets();
            }
            return Math.Min(numSockets, MAX_BASIC_STONES);
        }
        return 0;
    }

    public bool IsStorableInWarehouse()
    {
        return (GetItemMask() & Aion.GameServer.Model.Items.ItemMask.STORABLE_IN_WH) == Aion.GameServer.Model.Items.ItemMask.STORABLE_IN_WH;
    }

    public bool IsStorableInAccWarehouse()
    {
        return (GetItemMask() & Aion.GameServer.Model.Items.ItemMask.STORABLE_IN_AWH) == Aion.GameServer.Model.Items.ItemMask.STORABLE_IN_AWH && !IsSoulBound();
    }

    public bool IsStorableInLegWarehouse()
    {
        return (GetItemMask() & Aion.GameServer.Model.Items.ItemMask.STORABLE_IN_LWH) == Aion.GameServer.Model.Items.ItemMask.STORABLE_IN_LWH && !IsSoulBound();
    }

    public bool IsTradeable()
    {
        return (GetItemMask() & Aion.GameServer.Model.Items.ItemMask.TRADEABLE) == Aion.GameServer.Model.Items.ItemMask.TRADEABLE && !IsSoulBound();
    }

    public bool IsLegionTradeable()
    {
        return (GetItemMask() & Aion.GameServer.Model.Items.ItemMask.LEGION_TRADEABLE) == Aion.GameServer.Model.Items.ItemMask.LEGION_TRADEABLE && !IsSoulBound();
    }

    public bool IsRemodelable()
    {
        return (GetItemMask() & Aion.GameServer.Model.Items.ItemMask.REMODELABLE) == Aion.GameServer.Model.Items.ItemMask.REMODELABLE;
    }

    public bool IsSellable()
    {
        return (GetItemMask() & Aion.GameServer.Model.Items.ItemMask.SELLABLE) == Aion.GameServer.Model.Items.ItemMask.SELLABLE;
    }

    public bool CanApExtract()
    {
        return (GetItemMask() & Aion.GameServer.Model.Items.ItemMask.CAN_AP_EXTRACT) == Aion.GameServer.Model.Items.ItemMask.CAN_AP_EXTRACT;
    }

    public bool CanSocketGodstone()
    {
        return (GetItemMask() & Aion.GameServer.Model.Items.ItemMask.CAN_PROC_ENCHANT) == Aion.GameServer.Model.Items.ItemMask.CAN_PROC_ENCHANT;
    }

    /// <summary>Returns the expireTime.</summary>
    public int GetExpireTime()
    {
        return expireTime;
    }

    /// <summary>Returns the temporaryExchangeTime.</summary>
    public int GetTemporaryExchangeTime()
    {
        return temporaryExchangeTime;
    }

    public int GetTemporaryExchangeTimeRemaining()
    {
        if (temporaryExchangeTime == 0)
            return 0;
        return temporaryExchangeTime - (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000);
    }

    /// <param name="temporaryExchangeTime">The temporaryExchangeTime to set.</param>
    public void SetTemporaryExchangeTime(int temporaryExchangeTime)
    {
        this.temporaryExchangeTime = temporaryExchangeTime;
    }

    public void OnExpire(Aion.GameServer.Model.GameObjects.Player.Player player)
    {
        if (IsEquipped())
            player.GetEquipment().UnEquipItem(GetObjectId());

        foreach (Aion.GameServer.Model.Items.Storage.StorageType i in Aion.GameServer.Model.Items.Storage.StorageType.Values())
        {
            if (i == Aion.GameServer.Model.Items.Storage.StorageType.LEGION_WAREHOUSE)
                continue;
            Aion.GameServer.Model.Items.Storage.IStorage storage = player.GetStorage(i.GetId());

            if (storage != null && storage.GetItemByObjId(GetObjectId()) != null)
            {
                storage.Delete(this);
                if (i == Aion.GameServer.Model.Items.Storage.StorageType.CUBE)
                {
                    Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_DELETE_CASH_ITEM_BY_TIMEOUT(GetL10n()));
                }
                else if (i == Aion.GameServer.Model.Items.Storage.StorageType.ACCOUNT_WAREHOUSE || i == Aion.GameServer.Model.Items.Storage.StorageType.REGULAR_WAREHOUSE)
                {
                    Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_DELETE_CASH_ITEM_BY_TIMEOUT_IN_WAREHOUSE(GetL10n()));
                }
            }
        }
    }

    public void OnBeforeExpire(Aion.GameServer.Model.GameObjects.Player.Player player, int remainingMinutes)
    {
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_CASH_ITEM_TIME_LEFT(GetL10n(), remainingMinutes));
    }

    public void SetRepurchasePrice(long price)
    {
        repurchasePrice = price;
    }

    public long GetRepurchasePrice()
    {
        return repurchasePrice;
    }

    public int GetActivationCount()
    {
        return activationCount;
    }

    public void SetActivationCount(int activationCount)
    {
        this.activationCount = activationCount;
    }

    public Aion.GameServer.Model.Items.ChargeInfo GetConditioningInfo()
    {
        return conditioningInfo;
    }

    public int GetChargePoints()
    {
        return conditioningInfo != null ? conditioningInfo.GetChargePoints() : 0;
    }

    public int GetChargeLevel()
    {
        if (GetChargePoints() == 0)
            return 0;
        return GetChargePoints() > Aion.GameServer.Model.Items.ChargeInfo.LEVEL1 ? 2 : 1;
    }

    /// <summary>Calculate charge level based on main item and fusioned item.</summary>
    public int CalculateMaxChargeLevel()
    {
        int chargeLevel = 0;
        if (GetImprovement() != null)
            chargeLevel = GetImprovement().GetLevel();

        int fusionedChargeLevel = 0;
        if (HasFusionedItem() && fusionedItemTemplate.GetImprovement() != null)
            fusionedChargeLevel = fusionedItemTemplate.GetImprovement().GetLevel();
        return Math.Max(chargeLevel, fusionedChargeLevel);
    }

    /// <summary>Check for disabled charge levels due to recommend rank restriction.</summary>
    public int CalculateAvailableChargeLevel(Aion.GameServer.Model.GameObjects.Player.Player player)
    {
        int maxAvailableChargeLevel = CalculateMaxChargeLevel();
        Aion.GameServer.Model.Templates.Item.ItemUseLimits limits = HasFusionedItem() && fusionedItemTemplate.GetLevel() > itemTemplate.GetLevel()
            ? fusionedItemTemplate.GetUseLimits()
            : itemTemplate.GetUseLimits();
        if (limits.GetRecommendRank() > 0)
        {
            int rankLevelDiff = Math.Max(0, limits.GetRecommendRank() - player.GetAbyssRank().GetRank().GetId());
            maxAvailableChargeLevel -= rankLevelDiff;
        }
        return Math.Max(0, maxAvailableChargeLevel);
    }

    public Aion.GameServer.Model.Templates.Item.Improvement GetImprovement()
    {
        if (itemTemplate.GetImprovement() != null)
            return itemTemplate.GetImprovement();
        else if (HasFusionedItem() && fusionedItemTemplate.GetImprovement() != null)
            return fusionedItemTemplate.GetImprovement();
        return null;
    }

    public List<Aion.GameServer.Model.Stats.Calc.Functions.StatFunction> GetCurrentModifiers()
    {
        if (currentModifiers == null)
            currentModifiers = new List<Aion.GameServer.Model.Stats.Calc.Functions.StatFunction>();
        return currentModifiers;
    }

    public void SetCurrentModifiers(List<Aion.GameServer.Model.Stats.Calc.Functions.StatFunction> currentModifiers)
    {
        GetCurrentModifiers().Clear();
        GetCurrentModifiers().AddRange(currentModifiers);
    }

    public Aion.GameServer.Model.Items.IdianStone GetIdianStone()
    {
        return idianStone;
    }

    public void SetIdianStone(Aion.GameServer.Model.Items.IdianStone idianStone)
    {
        this.idianStone = idianStone;
    }

    public int GetBonusStatsId()
    {
        return bonusStatsEffect == null ? 0 : bonusStatsEffect.GetStatBonusId();
    }

    public Aion.GameServer.Model.Items.RandomBonusEffect GetBonusStatsEffect()
    {
        return bonusStatsEffect;
    }

    /// <summary>Must only be called while the item is unequipped, otherwise the old stats will remain active.</summary>
    public void SetBonusStats(int statBonusId, bool validate)
    {
        if (validate && isEquipped)
            log.LogWarning(new InvalidOperationException(), GetItemId() + " was equipped while switching bonus stats from " + GetBonusStatsId() + " to " + statBonusId);
        if (statBonusId == 0)
            bonusStatsEffect = null;
        else
            bonusStatsEffect = new Aion.GameServer.Model.Items.RandomBonusEffect(Aion.GameServer.Model.Templates.Item.Bonuses.StatBonusType.INVENTORY, itemTemplate.GetStatBonusSetId(), statBonusId);
    }

    public int GetTuneCount()
    {
        return tuneCount;
    }

    public void SetTuneCount(int tuneCount)
    {
        this.tuneCount = tuneCount;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    public void RemoveRemainingTuningCountIfPossible()
    {
        if (IsIdentified() && itemTemplate.GetMaxTuneCount() > 0 && tuneCount != itemTemplate.GetMaxTuneCount())
            SetTuneCount(itemTemplate.GetMaxTuneCount());
    }

    /// <summary>False if the item must be identified (tuned) before it can be equipped.</summary>
    public bool IsIdentified()
    {
        return tuneCount != -1;
    }

    public int GetFusionedItemBonusStatsId()
    {
        return fusionedItemBonusStatsEffect == null ? 0 : fusionedItemBonusStatsEffect.GetStatBonusId();
    }

    public Aion.GameServer.Model.Items.RandomBonusEffect GetFusionedItemBonusStatsEffect()
    {
        return fusionedItemBonusStatsEffect;
    }

    /// <summary>Must only be called while the item is unequipped, otherwise the old stats will remain active.</summary>
    public void SetFusionedItemBonusStats(int statBonusId, bool validate)
    {
        if (validate && isEquipped)
            log.LogWarning(new InvalidOperationException(), GetItemId() + " was equipped while switching fusioned bonus stats from " + GetFusionedItemBonusStatsId() + " to " + statBonusId);
        if (statBonusId == 0)
            fusionedItemBonusStatsEffect = null;
        else
            fusionedItemBonusStatsEffect = new Aion.GameServer.Model.Items.RandomBonusEffect(Aion.GameServer.Model.Templates.Item.Bonuses.StatBonusType.INVENTORY, fusionedItemTemplate.GetStatBonusSetId(), statBonusId);
    }

    public void SetTemperingEffect(Aion.GameServer.Model.Enchants.TemperingEffect temperingEffect)
    {
        this.temperingEffect = temperingEffect;
    }

    public Aion.GameServer.Model.Enchants.TemperingEffect GetTemperingEffect()
    {
        return temperingEffect;
    }

    public void SetEnchantEffect(Aion.GameServer.Model.Enchants.EnchantEffect enchantEffect)
    {
        this.enchantEffect = enchantEffect;
    }

    public Aion.GameServer.Model.Enchants.EnchantEffect GetEnchantEffect()
    {
        return enchantEffect;
    }

    public int GetPackCount()
    {
        return packCount;
    }

    public void SetPackCount(int packCount)
    {
        this.packCount = packCount;
    }

    public int GetMaxEnchantLevel()
    {
        return this.GetItemTemplate().GetMaxEnchantLevel() + this.GetEnchantBonus();
    }

    public int GetItemEnchantParam()
    {
        if (this.GetItemTemplate().IsWeapon())
        {
            if (this.GetEnchantLevel() >= 5 && this.GetEnchantLevel() < 10)
                return 1;
            else if (this.GetEnchantLevel() >= GetMaxEnchantLevel() && this.GetEnchantLevel() < 20)
                return 2;
            else if (this.GetEnchantLevel() >= 20)
                return 20;
        }
        else
        {
            if (this.GetTempering() >= 5 && this.GetTempering() < 10)
                return 10;
            else if (this.GetTempering() >= 10)
                return 20;
        }
        return this.GetTempering();
    }

    public bool IsAmplified()
    {
        return isAmplified;
    }

    public void SetAmplified(bool isAmplified)
    {
        this.isAmplified = isAmplified;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    public int GetBuffSkill()
    {
        return buffSkill;
    }

    public void SetBuffSkill(int buffSkill)
    {
        this.buffSkill = buffSkill;
    }

    public int GetRndPlumeBonusValue()
    {
        return rndPlumeBonusValue;
    }

    public void SetRndPlumeBonusValue(int rndPlumeBonusValue)
    {
        this.rndPlumeBonusValue = rndPlumeBonusValue;
    }

    public Aion.GameServer.Model.Items.PendingTuneResult GetPendingTuneResult()
    {
        return pendingTuneResult;
    }

    public void SetPendingTuneResult(Aion.GameServer.Model.Items.PendingTuneResult pendingTuneResult)
    {
        this.pendingTuneResult = pendingTuneResult;
    }

    public override string ToString()
    {
        return "Item [getItemId()=" + GetItemId() + ", getObjectId()=" + GetObjectId() + ", itemCount=" + itemCount + ", itemColor=" + itemColor
            + ", colorExpireTime=" + colorExpireTime + ", itemCreator=" + itemCreator + ", itemSkinId=" + GetItemSkinTemplate().GetTemplateId()
            + ", getFusionedItemId()=" + GetFusionedItemId() + ", isEquipped=" + isEquipped + ", manaStones=" + manaStones + ", fusionStones="
            + fusionStones + ", optionalSockets=" + optionalSockets + ", fusionedItemOptionalSockets=" + fusionedItemOptionalSockets + ", getGodStoneId()="
            + GetGodStoneId() + ", isSoulBound=" + isSoulBound + ", itemLocation=" + itemLocation + ", enchantLevel=" + enchantLevel + ", enchantBonus="
            + enchantBonus + ", expireTime=" + expireTime + ", temporaryExchangeTime=" + temporaryExchangeTime + ", repurchasePrice=" + repurchasePrice
            + ", activationCount=" + activationCount + ", bonusNumber=" + GetBonusStatsId() + ", tuneCount=" + tuneCount + ", packCount=" + packCount
            + ", tempering=" + tempering + ", isAmplified=" + isAmplified + ", buffSkill=" + buffSkill + ", rndPlumeBonusValue=" + rndPlumeBonusValue
            + ", getChargePoints()=" + GetChargePoints() + "]";
    }
}
