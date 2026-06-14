using System;
using System.Collections.Generic;
using System.Linq;

namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>
/// Java parity bridge: exposes the reworked-DTO shaped collections consumed by the player/item
/// persistence repository (PlayerEnterWorldRepository) on top of the faithful Player sub-objects.
/// These are read-only adapters over the faithful spine (GetSkillCoolDowns/GetItemCoolDowns/etc.),
/// keeping the existing DAO-style save helpers compiling without duplicating state.
/// </summary>
public partial class Player
{
    // Java parity: dao/InventoryDAO.store(player) flushes the cube/equipment storage rows.
    // Live consumers: SmInventoryInfo / SmStatsInfo / PlayerVisualStatsUpdateService / WorldNpcLootService.
    public IEnumerable<Aion.GameServer.Model.GameObjects.InventoryItem> InventoryItems =>
        GetInventory().GetItemsWithKinah().Select(MapItemToInventoryItem);

    // Java parity: mirrors the column mapping in dao/InventoryDAO.InsertItems/UpdateItems so the
    // reworked snapshot writers persist identical row values from the faithful Item spine.
    private Aion.GameServer.Model.GameObjects.InventoryItem MapItemToInventoryItem(Item item) =>
        new Aion.GameServer.Model.GameObjects.InventoryItem
        {
            ObjectId = item.GetObjectId(),
            ItemId = item.GetItemTemplate().GetTemplateId(),
            Count = item.GetItemCount(),
            Color = item.GetItemColor(),
            ColorExpires = item.GetColorExpireTime(),
            Creator = item.GetItemCreator(),
            ExpireTime = item.GetExpireTime(),
            ActivationCount = item.GetActivationCount(),
            OwnerId = ResolveStoreOwnerId(item.GetItemLocation()),
            IsEquipped = item.IsEquipped(),
            IsSoulBound = item.IsSoulBound(),
            Slot = item.GetEquipmentSlot(),
            Location = item.GetItemLocation(),
            Enchant = item.GetEnchantLevel(),
            EnchantBonus = item.GetEnchantBonus(),
            ItemSkin = item.GetItemSkinTemplate().GetTemplateId(),
            FusionedItem = item.GetFusionedItemId(),
            OptionalSocket = item.GetOptionalSockets(),
            OptionalFusionSocket = item.GetFusionedItemOptionalSockets(),
            Charge = item.GetChargePoints(),
            TuneCount = item.GetTuneCount(),
            RandomBonus = item.GetBonusStatsId(),
            FusionRandomBonus = item.GetFusionedItemBonusStatsId(),
            Tempering = item.GetTempering(),
            PackCount = item.GetPackCount(),
            IsAmplified = item.IsAmplified(),
            BuffSkill = item.GetBuffSkill(),
            RandomPlumeBonus = item.GetRndPlumeBonusValue(),
            PersistentState = MapPersistentState(item.GetPersistentState()),
            ManaStones = item.GetItemStones()
                .Select(s => new Aion.GameServer.Model.GameObjects.ItemStoneSocket(s.GetItemId(), s.GetSlot()))
                .ToList(),
            FusionStones = item.GetFusionStones()
                .Select(s => new Aion.GameServer.Model.GameObjects.ItemStoneSocket(s.GetItemId(), s.GetSlot()))
                .ToList(),
            Godstone = item.GetGodStone() == null
                ? null
                : new Aion.GameServer.Model.GameObjects.PlayerGodstone(
                    item.GetGodStone().GetItemId(),
                    item.GetGodStone().GetActivatedCount()),
            IdianStone = item.GetIdianStone() == null
                ? null
                : new Aion.GameServer.Model.GameObjects.PlayerIdianStone(
                    item.GetIdianStone().GetItemId(),
                    item.GetIdianStone().GetPolishNumber(),
                    item.GetIdianStone().GetPolishCharge()),
        };

    // Java parity: dao/InventoryDAO.getItemOwnerId resolves the owning id by storage location.
    private int ResolveStoreOwnerId(int location)
    {
        if (location == Aion.GameServer.Model.Items.Storage.StorageType.ACCOUNT_WAREHOUSE.GetId())
            return GetAccount() != null ? GetAccount().GetId() : GetObjectId();
        if (location == Aion.GameServer.Model.Items.Storage.StorageType.LEGION_WAREHOUSE.GetId())
            return GetLegion() != null ? GetLegion().GetLegionId() : GetObjectId();
        return GetObjectId();
    }

    private static Aion.GameServer.Model.GameObjects.InventoryItemPersistentState MapPersistentState(
        Aion.GameServer.Model.GameObjects.IPersistable.PersistentState state) =>
        state switch
        {
            Aion.GameServer.Model.GameObjects.IPersistable.PersistentState.NEW =>
                Aion.GameServer.Model.GameObjects.InventoryItemPersistentState.New,
            Aion.GameServer.Model.GameObjects.IPersistable.PersistentState.UPDATE_REQUIRED =>
                Aion.GameServer.Model.GameObjects.InventoryItemPersistentState.UpdateRequired,
            Aion.GameServer.Model.GameObjects.IPersistable.PersistentState.UPDATED =>
                Aion.GameServer.Model.GameObjects.InventoryItemPersistentState.Updated,
            Aion.GameServer.Model.GameObjects.IPersistable.PersistentState.DELETED =>
                Aion.GameServer.Model.GameObjects.InventoryItemPersistentState.Deleted,
            _ => Aion.GameServer.Model.GameObjects.InventoryItemPersistentState.NoAction,
        };

}
