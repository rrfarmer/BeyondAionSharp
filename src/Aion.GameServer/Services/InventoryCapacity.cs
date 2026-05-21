using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class InventoryCapacity
{
	private const int CubeStorageId = 0;
	private const int KinahItemId = 182400001;
	private const int BaseCubeSlots = 27;
	private const int CubeSlotsPerExpansion = 9;

	public static int GetCubeLimit(Player player)
	{
		// Java parity: model/items/storage/StorageType.CUBE plus Player.setCubeLimit.
		return BaseCubeSlots + (player.NpcExpands + player.QuestExpands + player.ItemExpands) * CubeSlotsPerExpansion;
	}

	public static int GetUsedCubeSlots(Player player)
	{
		// Java parity: model/items/storage/ItemStorage.getCubeItems, excluding kinah and equipped rows in the C# flattened inventory model.
		return player.InventoryItems.Count(item => item.Location == CubeStorageId && !item.IsEquipped && item.ItemId != KinahItemId);
	}

	public static int GetFreeCubeSlots(Player player)
	{
		return Math.Max(0, GetCubeLimit(player) - GetUsedCubeSlots(player));
	}

	public static bool HasFreeCubeSlot(Player player)
	{
		return GetFreeCubeSlots(player) > 0;
	}
}
