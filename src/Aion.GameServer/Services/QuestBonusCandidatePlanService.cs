using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public sealed class QuestBonusCandidatePlanService
{
	public QuestBonusCandidatePlan CreatePlan(
		QuestBonusCandidatePlanInput input,
		IEnumerable<QuestBonusItemGroupProjection> groups,
		ItemTemplateTable itemTemplates)
	{
		ArgumentNullException.ThrowIfNull(input);
		ArgumentNullException.ThrowIfNull(groups);
		ArgumentNullException.ThrowIfNull(itemTemplates);

		// Java parity: services/reward/BonusService#getMatchingItemsOfRandomGroup.
		// This is intentionally non-live: it applies deterministic item filters only.
		// Chance.selectElement, retry ordering, handler events, and item creation stay disabled.
		var normalizedBonusType = Normalize(input.BonusType);
		var normalizedRace = Normalize(input.PlayerRace);
		var candidateGroups = new List<QuestBonusCandidateGroupDescriptor>();
		var skippedItems = new List<QuestBonusSkippedItemDescriptor>();

		foreach (var group in groups.Where(group => string.Equals(Normalize(group.BonusType), normalizedBonusType, StringComparison.Ordinal)))
		{
			var candidates = new List<QuestBonusCandidateItemDescriptor>();

			foreach (var item in group.Items)
			{
				var template = itemTemplates.GetItemTemplate(item.ItemId);
				var skipReason = GetSkipReason(input, normalizedRace, group.ItemShape, item, template);
				if (skipReason != QuestBonusCandidateSkipReason.None)
				{
					skippedItems.Add(new QuestBonusSkippedItemDescriptor(
						group.ElementName,
						group.BonusType,
						group.ItemShape,
						item.ItemId,
						skipReason,
						item.Race,
						template?.Race,
						template?.Level,
						item.Level,
						item.Skill,
						item.MinLevel,
						item.MaxLevel));
					continue;
				}

				candidates.Add(CreateCandidate(group, item, template!));
			}

			if (candidates.Count > 0)
			{
				candidateGroups.Add(new QuestBonusCandidateGroupDescriptor(
					group.ElementName,
					group.BonusType,
					group.Chance,
					group.ItemShape,
					candidates));
			}
		}

		return new QuestBonusCandidatePlan(
			input,
			candidateGroups,
			skippedItems);
	}

	private static QuestBonusCandidateSkipReason GetSkipReason(
		QuestBonusCandidatePlanInput input,
		string normalizedRace,
		QuestBonusItemShape shape,
		QuestBonusItemProjection item,
		ItemTemplateSummary? template)
	{
		if (template == null)
			return QuestBonusCandidateSkipReason.MissingItemTemplate;

		var templateRace = Normalize(template.Race);
		if (templateRace != "PC_ALL" && !string.Equals(templateRace, normalizedRace, StringComparison.Ordinal))
			return QuestBonusCandidateSkipReason.TemplateRaceMismatch;

		var xmlRace = Normalize(item.Race);
		if (xmlRace != "PC_ALL" && !string.Equals(xmlRace, normalizedRace, StringComparison.Ordinal))
			return QuestBonusCandidateSkipReason.XmlRaceMismatch;

		if (!MatchesLevel(shape, item, template, input.BonusLevel))
			return QuestBonusCandidateSkipReason.BonusLevelMismatch;

		return shape switch
		{
			QuestBonusItemShape.CraftItem => GetCraftItemSkipReason(input, item),
			QuestBonusItemShape.CraftRecipe => GetCraftRecipeSkipReason(input, item),
			_ => QuestBonusCandidateSkipReason.None,
		};
	}

	private static QuestBonusCandidateSkipReason GetCraftItemSkipReason(QuestBonusCandidatePlanInput input, QuestBonusItemProjection item)
	{
		if (input.CombineSkill != (item.Skill ?? 0))
			return QuestBonusCandidateSkipReason.CraftSkillMismatch;
		if (input.CombineSkillPoint < (item.MinLevel ?? 0))
			return QuestBonusCandidateSkipReason.CraftSkillPointTooLow;
		if (input.CombineSkillPoint > (item.MaxLevel ?? 0))
			return QuestBonusCandidateSkipReason.CraftSkillPointTooHigh;

		return QuestBonusCandidateSkipReason.None;
	}

	private static QuestBonusCandidateSkipReason GetCraftRecipeSkipReason(QuestBonusCandidatePlanInput input, QuestBonusItemProjection item)
	{
		var level = item.Level ?? 0;
		if (input.CombineSkill != (item.Skill ?? 0))
			return QuestBonusCandidateSkipReason.CraftSkillMismatch;
		if (input.CombineSkillPoint < level)
			return QuestBonusCandidateSkipReason.CraftSkillPointTooLow;
		if (input.CombineSkillPoint > Math.Min(level + 40, level / 100 * 100 + 99))
			return QuestBonusCandidateSkipReason.CraftSkillPointTooHigh;

		return QuestBonusCandidateSkipReason.None;
	}

	private static bool MatchesLevel(
		QuestBonusItemShape shape,
		QuestBonusItemProjection item,
		ItemTemplateSummary template,
		int bonusLevel)
	{
		if (bonusLevel == 0)
			return true;

		return shape switch
		{
			QuestBonusItemShape.ItemRaceEntry => bonusLevel == template.Level,
			_ => bonusLevel == (item.Level ?? 0),
		};
	}

	private static QuestBonusCandidateItemDescriptor CreateCandidate(
		QuestBonusItemGroupProjection group,
		QuestBonusItemProjection item,
		ItemTemplateSummary template)
	{
		var count = GetCountRange(group.ItemShape, item);
		return new QuestBonusCandidateItemDescriptor(
			item.ItemId,
			item.Race,
			template.Race,
			template.Level,
			item.Level,
			item.Skill,
			item.MinLevel,
			item.MaxLevel,
			GetEffectiveChance(group.ItemShape, item),
			count.Min,
			count.Max,
			count.Mode);
	}

	private static float GetEffectiveChance(QuestBonusItemShape shape, QuestBonusItemProjection item) =>
		shape == QuestBonusItemShape.FullRewardItem ? item.Chance ?? 0f : 100f;

	private static (long Min, long Max, QuestBonusCandidateCountMode Mode) GetCountRange(
		QuestBonusItemShape shape,
		QuestBonusItemProjection item) =>
		shape switch
		{
			QuestBonusItemShape.FullRewardItem => (item.Count ?? 0L, item.Count ?? 0L, QuestBonusCandidateCountMode.Fixed),
			QuestBonusItemShape.CraftItem => (3L, 5L, QuestBonusCandidateCountMode.RandomInclusiveRange),
			QuestBonusItemShape.FoodItem => (5L, 10L, QuestBonusCandidateCountMode.RandomChoice),
			QuestBonusItemShape.MedicineItem => (1L, 3L, QuestBonusCandidateCountMode.RandomInclusiveRange),
			_ => (1L, 1L, QuestBonusCandidateCountMode.Fixed),
		};

	private static string Normalize(string? value) =>
		string.IsNullOrWhiteSpace(value) ? "PC_ALL" : value.Trim().ToUpperInvariant();
}

public sealed record QuestBonusCandidatePlanInput(
	string BonusType,
	int BonusLevel,
	string PlayerRace,
	int CombineSkill = 0,
	int CombineSkillPoint = 0);

public sealed record QuestBonusCandidatePlan(
	QuestBonusCandidatePlanInput Input,
	IReadOnlyList<QuestBonusCandidateGroupDescriptor> CandidateGroups,
	IReadOnlyList<QuestBonusSkippedItemDescriptor> SkippedItems)
{
	public int CandidateItemCount => CandidateGroups.Sum(group => group.Items.Count);
}

public sealed record QuestBonusCandidateGroupDescriptor(
	string ElementName,
	string BonusType,
	float Chance,
	QuestBonusItemShape ItemShape,
	IReadOnlyList<QuestBonusCandidateItemDescriptor> Items);

public sealed record QuestBonusCandidateItemDescriptor(
	int ItemId,
	string? XmlRace,
	string TemplateRace,
	int TemplateLevel,
	int? XmlLevel,
	int? Skill,
	int? MinLevel,
	int? MaxLevel,
	float EffectiveChance,
	long CountMin,
	long CountMax,
	QuestBonusCandidateCountMode CountMode);

public sealed record QuestBonusSkippedItemDescriptor(
	string GroupElementName,
	string BonusType,
	QuestBonusItemShape ItemShape,
	int ItemId,
	QuestBonusCandidateSkipReason Reason,
	string? XmlRace,
	string? TemplateRace,
	int? TemplateLevel,
	int? XmlLevel,
	int? Skill,
	int? MinLevel,
	int? MaxLevel);

public enum QuestBonusCandidateCountMode
{
	Fixed,
	RandomInclusiveRange,
	RandomChoice,
}

public enum QuestBonusCandidateSkipReason
{
	None,
	MissingItemTemplate,
	TemplateRaceMismatch,
	XmlRaceMismatch,
	BonusLevelMismatch,
	CraftSkillMismatch,
	CraftSkillPointTooLow,
	CraftSkillPointTooHigh,
}
