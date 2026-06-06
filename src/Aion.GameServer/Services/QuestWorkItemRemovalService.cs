using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public static class QuestWorkItemRemovalService
{
	public static IReadOnlyList<QuestWorkItemDeletion> RemoveQuestWorkItems(
		Player player,
		NearbyQuestTemplateSummary template,
		PlayerQuestState questState)
	{
		ArgumentNullException.ThrowIfNull(player);
		ArgumentNullException.ThrowIfNull(template);
		ArgumentNullException.ThrowIfNull(questState);

		// Java parity: QuestService.removeQuestWorkItems gets the full item count by item id, then
		// Storage.decreaseByItemId(..., qs.getStatus()) removes every matching cube stack.
		if (template.QuestWorkItems.Count == 0)
			return Array.Empty<QuestWorkItemDeletion>();

		var workItemIds = template.QuestWorkItems
			.Select(item => item.ItemId)
			.Distinct()
			.ToHashSet();
		var inventoryItems = player.InventoryItems.ToList();
		var deletions = new List<QuestWorkItemDeletion>();
		var deleteType = GetQuestDeleteType(questState.Status);

		foreach (var item in inventoryItems.Where(item => item.Location == 0 && !item.IsEquipped && workItemIds.Contains(item.ItemId)).ToArray())
		{
			inventoryItems.Remove(item);
			player.TrackDeletedItem(item);
			deletions.Add(new QuestWorkItemDeletion(
				item,
				deleteType,
				inventoryItems.Count(candidate => candidate.Location == 0 && !candidate.IsEquipped)));
		}

		if (deletions.Count != 0)
			player.InventoryItems = inventoryItems.ToArray();

		return deletions;
	}

	private static int GetQuestDeleteType(string questStatus)
	{
		return questStatus switch
		{
			"START" => SmDeleteItem.QuestStartDeleteType,
			"COMPLETE" => SmDeleteItem.QuestCompleteDeleteType,
			_ => 0,
		};
	}
}
