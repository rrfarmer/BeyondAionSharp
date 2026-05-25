using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum QuestCompletionFollowUpPlanStatus
{
	NoAction,
	Ready,
}

public enum QuestCompletionFollowUpDecision
{
	NoAction,
	Lock,
	Start,
}

public enum QuestCompletionFollowUpPacketAction
{
	Add,
	Update,
}

public sealed record QuestCompletionFollowUpRequest(
	int FollowUpQuestId,
	QuestCompletionFollowUpDecision Decision,
	PlayerQuestState? ExistingQuestState = null,
	bool StartConditionsEvaluatedByCaller = false,
	string JavaSource = "game-server/src/com/aionemu/gameserver/questEngine/handlers/AbstractQuestHandler.java#defaultOnQuestCompletedEvent");

public sealed record QuestCompletionFollowUpDescriptor(
	int Order,
	int FollowUpQuestId,
	string TargetQuestStatus,
	QuestCompletionFollowUpPacketAction PacketAction,
	string JavaSource,
	bool IsLive,
	bool StartConditionsEvaluatedByCaller,
	PlayerQuestState? ExistingQuestState = null);

public sealed record QuestCompletionFollowUpPlan(
	QuestCompletionFollowUpPlanStatus Status,
	IReadOnlyList<QuestCompletionFollowUpDescriptor> Descriptors)
{
	public bool HasOperations => Status == QuestCompletionFollowUpPlanStatus.Ready;
}

public static class QuestCompletionFollowUpPlanService
{
	public static QuestCompletionFollowUpPlan CreatePlan(
		IEnumerable<QuestCompletionFollowUpRequest> requests)
	{
		ArgumentNullException.ThrowIfNull(requests);

		var descriptors = new List<QuestCompletionFollowUpDescriptor>();
		var order = 1;

		foreach (var request in requests)
		{
			var targetStatus = GetTargetStatus(request.Decision);
			if (targetStatus is null)
				continue;
			if (request.ExistingQuestState?.Status == targetStatus)
				continue;

			descriptors.Add(new QuestCompletionFollowUpDescriptor(
				order++,
				request.FollowUpQuestId,
				targetStatus,
				GetPacketAction(request.ExistingQuestState),
				request.JavaSource,
				IsLive: false,
				request.StartConditionsEvaluatedByCaller,
				request.ExistingQuestState));
		}

		return new QuestCompletionFollowUpPlan(
			descriptors.Count == 0 ? QuestCompletionFollowUpPlanStatus.NoAction : QuestCompletionFollowUpPlanStatus.Ready,
			descriptors);
	}

	private static string? GetTargetStatus(QuestCompletionFollowUpDecision decision)
	{
		return decision switch
		{
			QuestCompletionFollowUpDecision.Lock => "LOCKED",
			QuestCompletionFollowUpDecision.Start => "START",
			_ => null,
		};
	}

	private static QuestCompletionFollowUpPacketAction GetPacketAction(PlayerQuestState? existingQuestState)
	{
		return existingQuestState is null || existingQuestState.Status == "COMPLETE"
			? QuestCompletionFollowUpPacketAction.Add
			: QuestCompletionFollowUpPacketAction.Update;
	}
}
