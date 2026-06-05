using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum QuestAbandonStatus
{
	MissingTemplate,
	CannotGiveup,
	MissingQuestState,
	AlreadyComplete,
	Locked,
	Deleted,
	ResetToComplete,
}

public sealed record QuestAbandonResult(
	QuestAbandonStatus Status,
	IReadOnlyList<SmQuestAction> TimerPackets,
	IReadOnlyList<QuestWorkItemDeletion> WorkItemDeletions,
	SmQuestAction? AbandonPacket,
	PlayerQuestState? OriginalQuestState,
	PlayerQuestState? FinalQuestState,
	bool NearbyQuestRefreshRequired)
{
	public bool Mutated => Status is QuestAbandonStatus.Deleted or QuestAbandonStatus.ResetToComplete;
}

public sealed record QuestWorkItemDeletion(InventoryItem Item, int DeleteType, int CubeItemCountAfterDeletion);

public static class QuestAbandonService
{
	public static QuestAbandonResult Abandon(Player player, int questId, NearbyQuestTemplateSummary? template)
	{
		// Java parity: CM_DELETE_QUEST.runImpl clears a visible quest timer before calling QuestService.abandonQuest.
		var timerPackets = new List<SmQuestAction>();
		if (template?.IsTimer == true)
			timerPackets.Add(SmQuestAction.Timer(questId, 0));

		if (template is null)
			return Result(QuestAbandonStatus.MissingTemplate, timerPackets, [], null, null, null, refresh: false);
		if (template.CannotGiveup)
			return Result(QuestAbandonStatus.CannotGiveup, timerPackets, [], null, null, null, refresh: false);

		var questState = player.Quests.FirstOrDefault(quest => quest.QuestId == questId);
		if (questState is null)
			return Result(QuestAbandonStatus.MissingQuestState, timerPackets, [], null, null, null, refresh: false);
		if (string.Equals(questState.Status, "COMPLETE", StringComparison.Ordinal))
			return Result(QuestAbandonStatus.AlreadyComplete, timerPackets, [], null, questState, questState, refresh: false);
		if (string.Equals(questState.Status, "LOCKED", StringComparison.Ordinal))
			return Result(QuestAbandonStatus.Locked, timerPackets, [], null, questState, questState, refresh: false);

		if (questState.CompleteCount > 0)
		{
			// Java: QuestState.setStatus(COMPLETE, false), setQuestVar(0), setFlags(0).
			var reset = questState with { Status = "COMPLETE", QuestVars = 0, Flags = 0 };
			player.Quests = player.Quests.Select(quest => quest.QuestId == questId ? reset : quest).ToArray();
			var workItemDeletions = RemoveQuestWorkItems(player, template, reset);
			return Result(QuestAbandonStatus.ResetToComplete, timerPackets, workItemDeletions, SmQuestAction.Abandon(reset), questState, reset, refresh: true);
		}

		player.Quests = player.Quests.Where(quest => quest.QuestId != questId).ToArray();
		var deletions = RemoveQuestWorkItems(player, template, questState);
		return Result(QuestAbandonStatus.Deleted, timerPackets, deletions, SmQuestAction.Abandon(questState), questState, null, refresh: true);
	}

	private static IReadOnlyList<QuestWorkItemDeletion> RemoveQuestWorkItems(
		Player player,
		NearbyQuestTemplateSummary template,
		PlayerQuestState questState)
	{
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

	private static QuestAbandonResult Result(
		QuestAbandonStatus status,
		IReadOnlyList<SmQuestAction> timerPackets,
		IReadOnlyList<QuestWorkItemDeletion> workItemDeletions,
		SmQuestAction? abandonPacket,
		PlayerQuestState? originalQuestState,
		PlayerQuestState? finalQuestState,
		bool refresh)
	{
		return new QuestAbandonResult(status, timerPackets, workItemDeletions, abandonPacket, originalQuestState, finalQuestState, refresh);
	}
}
