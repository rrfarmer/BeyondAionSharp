using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public static class IdianPolishBurnApplicationService
{
	public static IdianPolishBurnApplicationResult ApplyBurnPlan(
		Player player,
		IdianPolishBurnPlan plan,
		ItemTemplateTable itemTemplates)
	{
		// Java parity: model/items/IdianStone.decreasePolishCharge sends POLISH_CHARGE at the low-charge threshold and a full DEC_ITEM_USE update on exhaustion.
		if (!plan.Changed || plan.Burns.Count == 0)
			return IdianPolishBurnApplicationResult.NoChange();

		var inventoryItems = player.InventoryItems.ToList();
		var packets = new List<GameServerPacket>();
		foreach (var burn in plan.Burns)
		{
			ReplaceInventoryItem(inventoryItems, burn.ItemUpdate);
			var updateType = burn.UpdateKind switch
			{
				IdianPolishBurnUpdateKind.LowCharge => SmInventoryUpdateItem.PolishCharge,
				IdianPolishBurnUpdateKind.Exhausted => SmInventoryUpdateItem.DecreaseItemUse,
				_ => 0,
			};

			if (updateType == 0)
				continue;

			var template = itemTemplates.GetItemTemplate(burn.ItemUpdate.ItemId);
			if (template != null)
				packets.Add(new SmInventoryUpdateItem(burn.ItemUpdate, template, updateType));
		}

		player.InventoryItems = inventoryItems.ToArray();
		return new IdianPolishBurnApplicationResult(true, player.InventoryItems, packets);
	}

	private static void ReplaceInventoryItem(List<InventoryItem> items, InventoryItem update)
	{
		var index = items.FindIndex(item => item.ObjectId == update.ObjectId);
		if (index >= 0)
			items[index] = update;
		else
			items.Add(update);
	}
}

public sealed record IdianPolishBurnApplicationResult(
	bool Changed,
	IReadOnlyList<InventoryItem> InventoryItems,
	IReadOnlyList<GameServerPacket> Packets)
{
	public static IdianPolishBurnApplicationResult NoChange()
	{
		return new IdianPolishBurnApplicationResult(false, Array.Empty<InventoryItem>(), Array.Empty<GameServerPacket>());
	}
}
