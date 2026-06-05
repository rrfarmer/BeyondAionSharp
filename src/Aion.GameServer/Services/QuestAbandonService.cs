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
	IReadOnlyList<SmQuestAction> Packets,
	PlayerQuestState? OriginalQuestState,
	PlayerQuestState? FinalQuestState,
	bool NearbyQuestRefreshRequired)
{
	public bool Mutated => Status is QuestAbandonStatus.Deleted or QuestAbandonStatus.ResetToComplete;
}

public static class QuestAbandonService
{
	public static QuestAbandonResult Abandon(Player player, int questId, NearbyQuestTemplateSummary? template)
	{
		// Java parity: CM_DELETE_QUEST.runImpl clears a visible quest timer before calling QuestService.abandonQuest.
		var packets = new List<SmQuestAction>();
		if (template?.IsTimer == true)
			packets.Add(SmQuestAction.Timer(questId, 0));

		if (template is null)
			return Result(QuestAbandonStatus.MissingTemplate, packets, null, null, refresh: false);
		if (template.CannotGiveup)
			return Result(QuestAbandonStatus.CannotGiveup, packets, null, null, refresh: false);

		var questState = player.Quests.FirstOrDefault(quest => quest.QuestId == questId);
		if (questState is null)
			return Result(QuestAbandonStatus.MissingQuestState, packets, null, null, refresh: false);
		if (string.Equals(questState.Status, "COMPLETE", StringComparison.Ordinal))
			return Result(QuestAbandonStatus.AlreadyComplete, packets, questState, questState, refresh: false);
		if (string.Equals(questState.Status, "LOCKED", StringComparison.Ordinal))
			return Result(QuestAbandonStatus.Locked, packets, questState, questState, refresh: false);

		if (questState.CompleteCount > 0)
		{
			// Java: QuestState.setStatus(COMPLETE, false), setQuestVar(0), setFlags(0).
			var reset = questState with { Status = "COMPLETE", QuestVars = 0, Flags = 0 };
			player.Quests = player.Quests.Select(quest => quest.QuestId == questId ? reset : quest).ToArray();
			packets.Add(SmQuestAction.Abandon(reset));
			return Result(QuestAbandonStatus.ResetToComplete, packets, questState, reset, refresh: true);
		}

		player.Quests = player.Quests.Where(quest => quest.QuestId != questId).ToArray();
		packets.Add(SmQuestAction.Abandon(questState));
		return Result(QuestAbandonStatus.Deleted, packets, questState, null, refresh: true);
	}

	private static QuestAbandonResult Result(
		QuestAbandonStatus status,
		IReadOnlyList<SmQuestAction> packets,
		PlayerQuestState? originalQuestState,
		PlayerQuestState? finalQuestState,
		bool refresh)
	{
		return new QuestAbandonResult(status, packets, originalQuestState, finalQuestState, refresh);
	}
}
