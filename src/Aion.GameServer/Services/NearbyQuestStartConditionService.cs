using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class NearbyQuestStartConditionService
{
	public const int NearbyAllowedDiffToMinLevel = 2;

	public static NearbyQuestStartConditionResult CheckNearbyStartConditions(
		Player player,
		int questId,
		NearbyQuestTemplateTable questTemplates)
	{
		// Java parity breadcrumb: QuestService.checkStartConditions(player, questId, false, 2, false, false, false).
		if (!questTemplates.TryGetQuest(questId, out var template) || template == null)
			return NearbyQuestStartConditionResult.Fail(NearbyQuestStartConditionFailure.MissingTemplate);

		var questState = player.Quests.FirstOrDefault(quest => quest.QuestId == questId);
		if (questState != null)
		{
			if (string.Equals(questState.Status, "START", StringComparison.Ordinal)
				|| string.Equals(questState.Status, "REWARD", StringComparison.Ordinal))
				return NearbyQuestStartConditionResult.Fail(NearbyQuestStartConditionFailure.AlreadyStarted);

			if (string.Equals(questState.Status, "COMPLETE", StringComparison.Ordinal))
			{
				var repeatFailure = GetRepeatFailure(questState, template);
				if (repeatFailure != NearbyQuestStartConditionFailure.None)
					return NearbyQuestStartConditionResult.Fail(repeatFailure);
			}
		}

		if (!string.IsNullOrWhiteSpace(template.RacePermitted)
			&& !string.Equals(template.RacePermitted, "PC_ALL", StringComparison.Ordinal)
			&& !string.Equals(template.RacePermitted, player.Race, StringComparison.Ordinal))
			return NearbyQuestStartConditionResult.Fail(NearbyQuestStartConditionFailure.Race);

		var levelDiff = template.MinLevelPermitted - NearbyAllowedDiffToMinLevel - player.Level;
		if (levelDiff > 0)
			return NearbyQuestStartConditionResult.Fail(NearbyQuestStartConditionFailure.MinLevel);

		if (template.MaxLevelPermitted != 0 && player.Level > template.MaxLevelPermitted)
			return NearbyQuestStartConditionResult.Fail(NearbyQuestStartConditionFailure.MaxLevel);

		if (template.ClassPermitted.Count != 0 && !template.ClassPermitted.Contains(player.PlayerClass))
			return NearbyQuestStartConditionResult.Fail(NearbyQuestStartConditionFailure.Class);

		if (!string.IsNullOrWhiteSpace(template.GenderPermitted)
			&& !string.Equals(template.GenderPermitted, player.Gender, StringComparison.Ordinal))
			return NearbyQuestStartConditionResult.Fail(NearbyQuestStartConditionFailure.Gender);

		if (template.RequiredRank != 0 && player.AbyssRank.Rank < template.RequiredRank)
			return NearbyQuestStartConditionResult.Fail(NearbyQuestStartConditionFailure.Rank);

		var unsupportedFailure = GetUnsupportedDependencyFailure(template);
		if (unsupportedFailure != NearbyQuestStartConditionFailure.None)
			return NearbyQuestStartConditionResult.Fail(unsupportedFailure);

		return NearbyQuestStartConditionResult.Pass();
	}

	public static int GetLevelRequirementDiff(int questId, int playerLevel, NearbyQuestTemplateTable questTemplates)
	{
		// Java parity breadcrumb: QuestService.getLevelRequirementDiff returns 99 when DataManager.QUEST_DATA misses the template.
		return questTemplates.TryGetQuest(questId, out var template) && template != null
			? template.MinLevelPermitted - playerLevel
			: 99;
	}

	private static NearbyQuestStartConditionFailure GetRepeatFailure(
		PlayerQuestState questState,
		NearbyQuestTemplateSummary template)
	{
		// Java parity: QuestState.canRepeat blocks max-repeat exhaustion and time-based repeat cooldown.
		if (template.MaxRepeatCount != 255 && questState.CompleteCount >= template.MaxRepeatCount)
			return NearbyQuestStartConditionFailure.RepeatCount;

		return template.IsTimeBased
			? NearbyQuestStartConditionFailure.UnsupportedRepeatTiming
			: NearbyQuestStartConditionFailure.None;
	}

	private static NearbyQuestStartConditionFailure GetUnsupportedDependencyFailure(NearbyQuestTemplateSummary template)
	{
		if (template.HasXmlStartConditions)
			return NearbyQuestStartConditionFailure.UnsupportedXmlStartConditions;
		if (template.HasInventoryItems)
			return NearbyQuestStartConditionFailure.UnsupportedInventoryItems;
		if (template.CombineSkill != 0)
			return NearbyQuestStartConditionFailure.UnsupportedCombineSkill;
		if (template.NpcFactionId != 0)
			return NearbyQuestStartConditionFailure.UnsupportedNpcFaction;

		return NearbyQuestStartConditionFailure.None;
	}
}

public sealed record NearbyQuestStartConditionResult(bool CanStart, NearbyQuestStartConditionFailure Failure)
{
	public static NearbyQuestStartConditionResult Pass()
	{
		return new NearbyQuestStartConditionResult(true, NearbyQuestStartConditionFailure.None);
	}

	public static NearbyQuestStartConditionResult Fail(NearbyQuestStartConditionFailure failure)
	{
		return new NearbyQuestStartConditionResult(false, failure);
	}
}

public enum NearbyQuestStartConditionFailure
{
	None = 0,
	MissingTemplate = 1,
	AlreadyStarted = 2,
	RepeatCount = 3,
	UnsupportedRepeatTiming = 4,
	Race = 5,
	MinLevel = 6,
	MaxLevel = 7,
	Class = 8,
	Gender = 9,
	Rank = 10,
	UnsupportedXmlStartConditions = 11,
	UnsupportedInventoryItems = 12,
	UnsupportedCombineSkill = 13,
	UnsupportedNpcFaction = 14,
}
