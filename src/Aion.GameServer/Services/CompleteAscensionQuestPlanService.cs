using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum CompleteAscensionQuestPlanStatus
{
	PlanCreatedWithAdd,
	PlanCreatedWithUpdateOnly,
}

public sealed record CompleteAscensionQuestPlan(
	CompleteAscensionQuestPlanStatus Status,
	string Race,
	int QuestId,
	PlayerQuestState FinalQuestState,
	SmQuestAction? AddPacket,
	SmQuestAction UpdatePacket,
	bool ShouldSendAddFirst,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class CompleteAscensionQuestPlanService
{
	public const int ElyosQuestId = 1006;
	public const int AsmodiansQuestId = 2008;

	public static CompleteAscensionQuestPlan CreatePlan(string race, bool questStateExists)
	{
		// Java parity: services/ClassChangeService.completeAscensionQuest.
		// Live getQuestStateList().addQuest(), setStatus(), setQuestVar(), setRewardGroup() are deferred.
		var questId = string.Equals(race, "ELYOS", StringComparison.OrdinalIgnoreCase)
			? ElyosQuestId
			: AsmodiansQuestId;

		var finalState = new PlayerQuestState(questId, "COMPLETE", QuestVars: 0, Flags: 0, CompleteCount: 0);
		var updatePacket = SmQuestAction.Update(finalState);

		if (!questStateExists)
		{
			var addPacket = SmQuestAction.Add(finalState);
			return new CompleteAscensionQuestPlan(
				CompleteAscensionQuestPlanStatus.PlanCreatedWithAdd,
				race.ToUpperInvariant(),
				questId,
				finalState,
				addPacket,
				updatePacket,
				ShouldSendAddFirst: true,
				$"ClassChangeService.completeAscensionQuest -> questState==null -> SM_QUEST_ACTION(ADD, {questId}) -> SM_QUEST_ACTION(UPDATE, {questId})"
			);
		}

		return new CompleteAscensionQuestPlan(
			CompleteAscensionQuestPlanStatus.PlanCreatedWithUpdateOnly,
			race.ToUpperInvariant(),
			questId,
			finalState,
			AddPacket: null,
			updatePacket,
			ShouldSendAddFirst: false,
			$"ClassChangeService.completeAscensionQuest -> questState exists -> setStatus(COMPLETE) [deferred] -> SM_QUEST_ACTION(UPDATE, {questId})"
		);
	}
}
