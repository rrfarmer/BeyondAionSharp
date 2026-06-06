using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class InventoryCapacity
{
	private const int CubeStorageId = 0;
	private const int KinahItemId = 182400001;
	private const int BaseCubeSlots = 27;
	private const int CubeSlotsPerExpansion = 9;
	private const int WarehouseStorageId = 1;
	private const int BaseWarehouseSlots = 24;
	private const int WarehouseSlotsPerExpansion = 8;
	private const int AccountWarehouseStorageId = 2;
	private const int BaseAccountWarehouseSlots = 16;
	private const int SpecialCubeSlots = 102;

	public static int GetCubeLimit(Player player)
	{
		// Java parity: model/items/storage/StorageType.CUBE plus Player.setCubeLimit.
		return BaseCubeSlots + (player.NpcExpands + player.QuestExpands + player.ItemExpands) * CubeSlotsPerExpansion;
	}

	public static int GetUsedCubeSlots(Player player)
	{
		return GetUsedCubeSlots(player.InventoryItems);
	}

	public static int GetUsedCubeSlots(Player player, ItemTemplateTable itemTemplates)
	{
		return GetUsedCubeSlots(player.InventoryItems, itemTemplates);
	}

	public static int GetUsedCubeSlots(IReadOnlyList<InventoryItem> inventoryItems)
	{
		// Java parity: model/items/storage/ItemStorage.getCubeItems, excluding kinah and equipped rows in the C# flattened inventory model.
		return inventoryItems.Count(item => item.Location == CubeStorageId && !item.IsEquipped && item.ItemId != KinahItemId);
	}

	public static int GetUsedCubeSlots(IReadOnlyList<InventoryItem> inventoryItems, ItemTemplateTable itemTemplates)
	{
		return inventoryItems.Count(item => IsNormalCubeItem(item, itemTemplates));
	}

	public static int GetFreeCubeSlots(Player player)
	{
		return GetFreeCubeSlots(player, player.InventoryItems);
	}

	public static int GetFreeCubeSlots(Player player, IReadOnlyList<InventoryItem> inventoryItems)
	{
		return Math.Max(0, GetCubeLimit(player) - GetUsedCubeSlots(inventoryItems));
	}

	public static int GetFreeCubeSlots(Player player, ItemTemplateTable itemTemplates)
	{
		return Math.Max(0, GetCubeLimit(player) - GetUsedCubeSlots(player, itemTemplates));
	}

	public static int GetFreeCubeSlots(Player player, IReadOnlyList<InventoryItem> inventoryItems, ItemTemplateTable itemTemplates)
	{
		return Math.Max(0, GetCubeLimit(player) - GetUsedCubeSlots(inventoryItems, itemTemplates));
	}

	public static bool HasFreeCubeSlot(Player player)
	{
		return GetFreeCubeSlots(player) > 0;
	}

	public static bool HasFreeCubeSlot(Player player, ItemTemplateTable itemTemplates)
	{
		return GetFreeCubeSlots(player, itemTemplates) > 0;
	}

	public static int GetUsedSpecialCubeSlots(Player player, ItemTemplateTable itemTemplates)
	{
		return GetUsedSpecialCubeSlots(player.InventoryItems, itemTemplates);
	}

	public static int GetUsedSpecialCubeSlots(IReadOnlyList<InventoryItem> inventoryItems, ItemTemplateTable itemTemplates)
	{
		// Java parity: model/items/storage/ItemStorage.getSpecialCubeItems uses ItemTemplate.getExtraInventoryId() > 0.
		return inventoryItems.Count(item => IsSpecialCubeItem(item, itemTemplates));
	}

	public static int GetFreeSpecialCubeSlots(Player player, ItemTemplateTable itemTemplates)
	{
		return Math.Max(0, SpecialCubeSlots - GetUsedSpecialCubeSlots(player, itemTemplates));
	}

	public static int GetFreeSpecialCubeSlots(IReadOnlyList<InventoryItem> inventoryItems, ItemTemplateTable itemTemplates)
	{
		return Math.Max(0, SpecialCubeSlots - GetUsedSpecialCubeSlots(inventoryItems, itemTemplates));
	}

	public static bool HasFreeSpecialCubeSlot(Player player, ItemTemplateTable itemTemplates)
	{
		return GetFreeSpecialCubeSlots(player, itemTemplates) > 0;
	}

	public static int GetWarehouseLimit(Player player)
	{
		// Java parity: model/items/storage/StorageType.REGULAR_WAREHOUSE plus Player.setWarehouseLimit.
		return BaseWarehouseSlots + (player.WarehouseNpcExpands + player.WarehouseBonusExpands) * WarehouseSlotsPerExpansion;
	}

	public static int GetUsedWarehouseSlots(Player player)
	{
		return player.WarehouseItems.Count(item => item.Location == WarehouseStorageId && item.ItemId != KinahItemId);
	}

	public static int GetFreeWarehouseSlots(Player player)
	{
		return Math.Max(0, GetWarehouseLimit(player) - GetUsedWarehouseSlots(player));
	}

	public static int GetAccountWarehouseLimit()
	{
		// Java parity: model/items/storage/StorageType.ACCOUNT_WAREHOUSE.
		return BaseAccountWarehouseSlots;
	}

	public static int GetUsedAccountWarehouseSlots(Player player)
	{
		return player.AccountWarehouseItems.Count(item => item.Location == AccountWarehouseStorageId && item.ItemId != KinahItemId);
	}

	public static int GetFreeAccountWarehouseSlots(Player player)
	{
		return Math.Max(0, GetAccountWarehouseLimit() - GetUsedAccountWarehouseSlots(player));
	}

	private static bool IsNormalCubeItem(InventoryItem item, ItemTemplateTable itemTemplates)
	{
		if (item.Location != CubeStorageId || item.IsEquipped || item.ItemId == KinahItemId)
			return false;

		var template = itemTemplates.GetItemTemplate(item.ItemId);
		return template == null || template.ExtraInventoryId < 1;
	}

	private static bool IsSpecialCubeItem(InventoryItem item, ItemTemplateTable itemTemplates)
	{
		if (item.Location != CubeStorageId || item.IsEquipped || item.ItemId == KinahItemId)
			return false;

		return itemTemplates.GetItemTemplate(item.ItemId)?.ExtraInventoryId > 0;
	}
}
