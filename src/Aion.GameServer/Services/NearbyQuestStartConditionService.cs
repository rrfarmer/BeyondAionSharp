using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class NearbyQuestStartConditionService
{
	public const int NearbyAllowedDiffToMinLevel = 2;
	private static readonly int[] AnyCombineSkillIds = [30002, 30003, 40001, 40002, 40003, 40004, 40007, 40008, 40010];
	private static readonly int[] AnyCombineSkillIdsWithoutTapping = [40001, 40002, 40003, 40004, 40007, 40008, 40010];

	public static NearbyQuestStartConditionResult CheckNearbyStartConditions(
		Player player,
		int questId,
		NearbyQuestTemplateTable questTemplates,
		DateTimeOffset? now = null)
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
				var repeatFailure = GetRepeatFailure(questState, template, now ?? DateTimeOffset.Now);
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

		var xmlFailure = GetXmlStartConditionFailure(player, template, questTemplates);
		if (xmlFailure != NearbyQuestStartConditionFailure.None)
			return NearbyQuestStartConditionResult.Fail(xmlFailure);

		var inventoryFailure = GetInventoryItemFailure(player, template);
		if (inventoryFailure != NearbyQuestStartConditionFailure.None)
			return NearbyQuestStartConditionResult.Fail(inventoryFailure);

		var combineSkillFailure = GetCombineSkillFailure(player, template);
		if (combineSkillFailure != NearbyQuestStartConditionFailure.None)
			return NearbyQuestStartConditionResult.Fail(combineSkillFailure);

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
		NearbyQuestTemplateSummary template,
		DateTimeOffset now)
	{
		// Java parity: QuestState.canRepeat blocks max-repeat exhaustion and time-based repeat cooldown.
		if (template.MaxRepeatCount != 255 && questState.CompleteCount >= template.MaxRepeatCount)
			return NearbyQuestStartConditionFailure.RepeatCount;

		if (template.IsTimeBased
			&& questState.NextRepeatTime.HasValue
			&& now < questState.NextRepeatTime.Value)
			return NearbyQuestStartConditionFailure.RepeatTiming;

		return NearbyQuestStartConditionFailure.None;
	}

	private static NearbyQuestStartConditionFailure GetUnsupportedDependencyFailure(NearbyQuestTemplateSummary template)
	{
		if (template.HasXmlStartConditions
			&& (template.XmlStartConditions.Count == 0 || template.HasUnsupportedXmlStartConditionElements))
			return NearbyQuestStartConditionFailure.UnsupportedXmlStartConditions;
		if (template.HasInventoryItems && template.InventoryItems.Count == 0)
			return NearbyQuestStartConditionFailure.UnsupportedInventoryItems;
		if (template.NpcFactionId != 0)
			return NearbyQuestStartConditionFailure.UnsupportedNpcFaction;

		return NearbyQuestStartConditionFailure.None;
	}

	private static NearbyQuestStartConditionFailure GetCombineSkillFailure(
		Player player,
		NearbyQuestTemplateSummary template)
	{
		if (template.CombineSkill == 0)
			return NearbyQuestStartConditionFailure.None;

		// Java parity: QuestService.checkCombineSkill checks one explicit combine skill, or the
		// any-skill sentinel -1; NPC faction 12/13 excludes essence/aether tapping.
		var skillIds = template.CombineSkill == -1
			? template.NpcFactionId is 12 or 13 ? AnyCombineSkillIdsWithoutTapping : AnyCombineSkillIds
			: [template.CombineSkill];

		foreach (var skillId in skillIds)
		{
			var skill = player.Skills.FirstOrDefault(playerSkill => playerSkill.SkillId == skillId);
			if (skill == null || skill.SkillLevel < template.CombineSkillPoint)
				continue;

			if (string.Equals(template.QuestCategory, "TASK", StringComparison.Ordinal)
				&& skill.SkillLevel - 40 > template.CombineSkillPoint)
				continue;

			return NearbyQuestStartConditionFailure.None;
		}

		return NearbyQuestStartConditionFailure.CombineSkill;
	}

	private static NearbyQuestStartConditionFailure GetInventoryItemFailure(
		Player player,
		NearbyQuestTemplateSummary template)
	{
		if (!template.HasInventoryItems)
			return NearbyQuestStartConditionFailure.None;

		if (template.InventoryItems.Count == 0)
			return NearbyQuestStartConditionFailure.UnsupportedInventoryItems;

		// Java parity: QuestService.inventoryItemCheck checks getFirstItemByItemId only.
		// The XML count is intentionally not enforced for this start-condition gate.
		return template.InventoryItems.All(requiredItem =>
			player.InventoryItems.Any(item => item.ItemId == requiredItem.ItemId))
			? NearbyQuestStartConditionFailure.None
			: NearbyQuestStartConditionFailure.InventoryItems;
	}

	private static NearbyQuestStartConditionFailure GetXmlStartConditionFailure(
		Player player,
		NearbyQuestTemplateSummary template,
		NearbyQuestTemplateTable questTemplates)
	{
		if (!template.HasXmlStartConditions)
			return NearbyQuestStartConditionFailure.None;

		if (template.XmlStartConditions.Count == 0 || template.HasUnsupportedXmlStartConditionElements)
			return NearbyQuestStartConditionFailure.UnsupportedXmlStartConditions;

		var fulfilledStartConditions = template.XmlStartConditions.Count(condition =>
			CheckXmlStartCondition(player, condition, questTemplates));
		return fulfilledStartConditions < GetRequiredXmlStartConditionCount(template)
			? NearbyQuestStartConditionFailure.XmlStartConditions
			: NearbyQuestStartConditionFailure.None;
	}

	private static int GetRequiredXmlStartConditionCount(NearbyQuestTemplateSummary template)
	{
		// Java parity: QuestTemplate.getRequiredConditionCount requires every mandatory XMLStartCondition
		// and at most one optional condition containing <finished>. Master-crafting adjustment remains blocked
		// because combine-skill quest support is still outside the nearby staged predicate.
		var optionalCount = template.XmlStartConditions.Count(condition => condition.IsOptional);
		var mandatoryCount = template.XmlStartConditions.Count - optionalCount;
		return Math.Min(1, optionalCount) + mandatoryCount;
	}

	private static bool CheckXmlStartCondition(
		Player player,
		NearbyQuestXmlStartCondition condition,
		NearbyQuestTemplateTable questTemplates)
	{
		// Java parity breadcrumb: XMLStartCondition.check(player, warn=false). Equipped items pass when warn is false.
		return CheckFinishedQuests(player, condition, questTemplates)
			&& CheckUnfinishedQuests(player, condition)
			&& CheckNoAcquiredQuests(player, condition)
			&& CheckAcquiredQuests(player, condition)
			&& IsRequiredTitleDisplayed(player, condition);
	}

	private static bool CheckFinishedQuests(
		Player player,
		NearbyQuestXmlStartCondition condition,
		NearbyQuestTemplateTable questTemplates)
	{
		foreach (var finished in condition.Finished)
		{
			var questState = FindQuestState(player, finished.QuestId);
			if (questState == null || !string.Equals(questState.Status, "COMPLETE", StringComparison.Ordinal))
				return false;
			if (finished.Reward >= 0 && questState.RewardGroup != finished.Reward)
				return false;

			if (questTemplates.TryGetQuest(finished.QuestId, out var finishedTemplate)
				&& finishedTemplate != null
				&& finishedTemplate.MaxRepeatCount > 1
				&& finishedTemplate.MaxRepeatCount != 255
				&& questState.CompleteCount != finishedTemplate.MaxRepeatCount)
				return false;
		}

		return true;
	}

	private static bool CheckUnfinishedQuests(Player player, NearbyQuestXmlStartCondition condition)
	{
		return condition.Unfinished.All(questId =>
			!string.Equals(FindQuestState(player, questId)?.Status, "COMPLETE", StringComparison.Ordinal));
	}

	private static bool CheckNoAcquiredQuests(Player player, NearbyQuestXmlStartCondition condition)
	{
		return condition.NoAcquired.All(questId =>
		{
			var status = FindQuestState(player, questId)?.Status;
			return !string.Equals(status, "START", StringComparison.Ordinal)
				&& !string.Equals(status, "REWARD", StringComparison.Ordinal);
		});
	}

	private static bool CheckAcquiredQuests(Player player, NearbyQuestXmlStartCondition condition)
	{
		return condition.Acquired.All(questId =>
		{
			var status = FindQuestState(player, questId)?.Status;
			return status != null && !string.Equals(status, "LOCKED", StringComparison.Ordinal);
		});
	}

	private static bool IsRequiredTitleDisplayed(Player player, NearbyQuestXmlStartCondition condition)
	{
		return condition.RequiredTitle == 0 || player.TitleId == condition.RequiredTitle;
	}

	private static PlayerQuestState? FindQuestState(Player player, int questId)
	{
		return player.Quests.FirstOrDefault(quest => quest.QuestId == questId);
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
	RepeatTiming = 4,
	UnsupportedRepeatTiming = 5,
	Race = 6,
	MinLevel = 7,
	MaxLevel = 8,
	Class = 9,
	Gender = 10,
	Rank = 11,
	XmlStartConditions = 12,
	InventoryItems = 13,
	CombineSkill = 14,
	UnsupportedXmlStartConditions = 15,
	UnsupportedInventoryItems = 16,
	UnsupportedCombineSkill = 17,
	UnsupportedNpcFaction = 18,
}
