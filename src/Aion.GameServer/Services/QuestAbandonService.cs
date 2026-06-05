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
	IReadOnlyList<SmQuestAction> NpcFactionDailyQuestPackets,
	IReadOnlyList<QuestWorkItemDeletion> WorkItemDeletions,
	PlayerNpcFactionAbortResult? NpcFactionAbort,
	IReadOnlyList<PlayerNpcFactionState> NpcFactionPersistenceUpdates,
	int? WorkOrderRecipeId,
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
	public static QuestAbandonResult Abandon(
		Player player,
		int questId,
		NearbyQuestTemplateSummary? template,
		int? currentEpochSeconds = null,
		NearbyQuestTemplateTable? questTemplates = null,
		int? nextResetEpochSeconds = null,
		Func<int, int>? randomIndexSelector = null,
		Func<int, bool>? hasQuestHandler = null)
	{
		// Java parity: CM_DELETE_QUEST.runImpl clears a visible quest timer before calling QuestService.abandonQuest.
		var timerPackets = new List<SmQuestAction>();
		if (template?.IsTimer == true)
			timerPackets.Add(SmQuestAction.Timer(questId, 0));

		if (template is null)
			return Result(QuestAbandonStatus.MissingTemplate, timerPackets, [], [], null, [], null, null, null, null, refresh: false);
		if (template.CannotGiveup)
			return Result(QuestAbandonStatus.CannotGiveup, timerPackets, [], [], null, [], null, null, null, null, refresh: false);

		var questState = player.Quests.FirstOrDefault(quest => quest.QuestId == questId);
		if (questState is null)
			return Result(QuestAbandonStatus.MissingQuestState, timerPackets, [], [], null, [], null, null, null, null, refresh: false);
		if (string.Equals(questState.Status, "COMPLETE", StringComparison.Ordinal))
			return Result(QuestAbandonStatus.AlreadyComplete, timerPackets, [], [], null, [], null, null, questState, questState, refresh: false);
		if (string.Equals(questState.Status, "LOCKED", StringComparison.Ordinal))
			return Result(QuestAbandonStatus.Locked, timerPackets, [], [], null, [], null, null, questState, questState, refresh: false);

		if (questState.CompleteCount > 0)
		{
			// Java: QuestState.setStatus(COMPLETE, false), setQuestVar(0), setFlags(0).
			var reset = questState with { Status = "COMPLETE", QuestVars = 0, Flags = 0 };
			player.Quests = player.Quests.Select(quest => quest.QuestId == questId ? reset : quest).ToArray();
			var npcFactionAbort = AbortNpcFactionQuest(player, template);
			var dailyQuests = CreateDailyQuestPackets(npcFactionAbort, player, currentEpochSeconds, questTemplates, nextResetEpochSeconds, randomIndexSelector, hasQuestHandler);
			var workItemDeletions = RemoveQuestWorkItems(player, template, reset);
			return Result(QuestAbandonStatus.ResetToComplete, timerPackets, dailyQuests.Packets, workItemDeletions, npcFactionAbort, dailyQuests.PersistenceUpdates, GetWorkOrderRecipeId(template), SmQuestAction.Abandon(reset), questState, reset, refresh: true);
		}

		player.Quests = player.Quests.Where(quest => quest.QuestId != questId).ToArray();
		var factionAbort = AbortNpcFactionQuest(player, template);
		var factionDailyQuests = CreateDailyQuestPackets(factionAbort, player, currentEpochSeconds, questTemplates, nextResetEpochSeconds, randomIndexSelector, hasQuestHandler);
		var deletions = RemoveQuestWorkItems(player, template, questState);
		return Result(QuestAbandonStatus.Deleted, timerPackets, factionDailyQuests.Packets, deletions, factionAbort, factionDailyQuests.PersistenceUpdates, GetWorkOrderRecipeId(template), SmQuestAction.Abandon(questState), questState, null, refresh: true);
	}

	private static int? GetWorkOrderRecipeId(NearbyQuestTemplateSummary template)
	{
		// Java parity: QuestService.abandonQuest deletes a work-order recipe only for TASK
		// templates whose XMLQuest handler is WorkOrdersData.
		return string.Equals(template.QuestCategory, "TASK", StringComparison.Ordinal)
			&& template.WorkOrderRecipeId != 0
			? template.WorkOrderRecipeId
			: null;
	}

	private static PlayerNpcFactionAbortResult? AbortNpcFactionQuest(Player player, NearbyQuestTemplateSummary template)
	{
		// Java parity: QuestService.abandonQuest calls player.getNpcFactions().abortQuest(template)
		// immediately after the quest-list mutation and before quest work-item deletion.
		if (template.NpcFactionId == 0)
			return null;

		var abort = player.NpcFactions.AbortQuest(template.NpcFactionId);
		if (abort.Applied)
			player.NpcFactions = abort.Snapshot;

		return abort;
	}

	private static NpcFactionDailyQuestSelection CreateDailyQuestPackets(
		PlayerNpcFactionAbortResult? abort,
		Player player,
		int? currentEpochSeconds,
		NearbyQuestTemplateTable? questTemplates,
		int? nextResetEpochSeconds,
		Func<int, int>? randomIndexSelector,
		Func<int, bool>? hasQuestHandler)
	{
		// Java parity: NpcFactions.abortQuest calls sendDailyQuest immediately after setting
		// the faction state to NOTING. This mirrors the reusable assigned branch and the random
		// QuestsData.getQuestsByNpcFaction branch when the runtime quest table/reset time are available.
		if (abort?.Applied != true)
			return NpcFactionDailyQuestSelection.Empty;

		var now = currentEpochSeconds ?? CurrentEpochSeconds();
		var packets = new List<SmQuestAction>();
		var persistenceUpdates = new Dictionary<int, PlayerNpcFactionState>();
		if (abort.AbortedFaction != null)
			persistenceUpdates[abort.AbortedFaction.FactionId] = abort.AbortedFaction;

		for (var slot = 0; slot < 2; slot++)
		{
			var isMentorSlot = slot == 1;
			var faction = player.NpcFactions.GetActiveFaction(isMentorSlot);
			if (faction is not { IsActive: true })
				continue;

			if (!player.NpcFactions.CanStartQuest(isMentorSlot, now))
				continue;

			var questId = 0;
			var skipSlot = false;
			switch (faction.State)
			{
				case PlayerNpcFactionQuestState.Complete:
					if (faction.TimeEpochSeconds > now)
						skipSlot = true;
					break;
				case PlayerNpcFactionQuestState.Start:
					skipSlot = true;
					break;
				case PlayerNpcFactionQuestState.Noting:
					if (faction.TimeEpochSeconds > now)
						questId = faction.QuestId;
					break;
			}

			if (skipSlot)
				continue;

			if (questId == 0)
			{
				if (questTemplates == null || nextResetEpochSeconds == null)
					continue;

				var candidates = questTemplates
					.GetQuestsByNpcFaction(faction.FactionId)
					.Where(candidate => (hasQuestHandler?.Invoke(candidate.QuestId) ?? true)
						&& NearbyQuestStartConditionService.CheckNearbyStartConditions(
							player,
							candidate.QuestId,
							questTemplates,
							DateTimeOffset.FromUnixTimeSeconds(now)).CanStart)
					.ToArray();
				if (candidates.Length == 0)
					continue;

				var selected = candidates[SelectRandomIndex(candidates.Length, randomIndexSelector)];
				questId = selected.QuestId;
				var assignment = player.NpcFactions.AssignDailyQuest(faction.FactionId, questId, nextResetEpochSeconds.Value);
				if (assignment.Applied)
				{
					player.NpcFactions = assignment.Snapshot;
					persistenceUpdates[faction.FactionId] = assignment.AssignedFaction!;
				}
			}

			if (questId != 0)
				packets.Add(SmQuestAction.Unknown(questId));
		}

		return new NpcFactionDailyQuestSelection(packets, persistenceUpdates.Values.ToArray());
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
		IReadOnlyList<SmQuestAction> npcFactionDailyQuestPackets,
		IReadOnlyList<QuestWorkItemDeletion> workItemDeletions,
		PlayerNpcFactionAbortResult? npcFactionAbort,
		IReadOnlyList<PlayerNpcFactionState> npcFactionPersistenceUpdates,
		int? workOrderRecipeId,
		SmQuestAction? abandonPacket,
		PlayerQuestState? originalQuestState,
		PlayerQuestState? finalQuestState,
		bool refresh)
	{
		return new QuestAbandonResult(status, timerPackets, npcFactionDailyQuestPackets, workItemDeletions, npcFactionAbort, npcFactionPersistenceUpdates, workOrderRecipeId, abandonPacket, originalQuestState, finalQuestState, refresh);
	}

	private static int SelectRandomIndex(int count, Func<int, int>? randomIndexSelector)
	{
		var index = randomIndexSelector?.Invoke(count) ?? Random.Shared.Next(count);
		if (index < 0 || index >= count)
			throw new ArgumentOutOfRangeException(nameof(randomIndexSelector), "Random index selector returned an out-of-range index.");

		return index;
	}

	private static int CurrentEpochSeconds()
	{
		var epochSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		return epochSeconds > int.MaxValue ? int.MaxValue : (int)epochSeconds;
	}
}

internal sealed record NpcFactionDailyQuestSelection(
	IReadOnlyList<SmQuestAction> Packets,
	IReadOnlyList<PlayerNpcFactionState> PersistenceUpdates)
{
	public static NpcFactionDailyQuestSelection Empty { get; } = new(
		Array.Empty<SmQuestAction>(),
		Array.Empty<PlayerNpcFactionState>());
}
