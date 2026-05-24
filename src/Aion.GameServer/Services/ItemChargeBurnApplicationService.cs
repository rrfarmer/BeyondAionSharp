using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public static class ItemChargeBurnApplicationService
{
	public static ItemChargeBurnApplicationResult ApplyBurnPlan(
		Player player,
		ItemChargeBurnPlan plan,
		ItemTemplateTable itemTemplates)
	{
		// Java parity: model/items/ChargeInfo.sendItemUpdate only emits ItemUpdateType.CHARGE when updateChargePoints crosses a visual bar step.
		if (!plan.Changed || plan.Burns.Count == 0)
			return ItemChargeBurnApplicationResult.NoChange();

		var inventoryItems = player.InventoryItems.ToList();
		var packets = new List<GameServerPacket>();
		foreach (var burn in plan.Burns)
		{
			ReplaceInventoryItem(inventoryItems, burn.ItemUpdate);
			if (!burn.ChargeBarChanged)
				continue;

			var template = itemTemplates.GetItemTemplate(burn.ItemUpdate.ItemId);
			if (template != null)
				packets.Add(new SmInventoryUpdateItem(burn.ItemUpdate, template, SmInventoryUpdateItem.Charge));
		}

		player.InventoryItems = inventoryItems.ToArray();
		return new ItemChargeBurnApplicationResult(true, player.InventoryItems, packets);
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

public sealed record ItemChargeBurnApplicationResult(
	bool Changed,
	IReadOnlyList<InventoryItem> InventoryItems,
	IReadOnlyList<GameServerPacket> Packets)
{
	public static ItemChargeBurnApplicationResult NoChange()
	{
		return new ItemChargeBurnApplicationResult(false, Array.Empty<InventoryItem>(), Array.Empty<GameServerPacket>());
	}
}
