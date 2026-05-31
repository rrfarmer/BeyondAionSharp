using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public static class SkillStatChangeConditionReadinessReportService
{
	public static SkillStatChangeConditionReadinessReport CreateReport(
		SkillTemplateTable? skillTemplates,
		bool hasLiveConditionValidatorProvider = false)
	{
		var missingInputs = new List<string>();
		var conditionNameCounts = new Dictionary<string, int>(StringComparer.Ordinal);
		var conditionedChangeCount = 0;
		var conditionEntryCount = 0;

		if (skillTemplates == null)
		{
			missingInputs.Add("skill_templates");
		}
		else
		{
			foreach (var template in skillTemplates.Templates)
			{
				foreach (var change in EnumerateStatChanges(template))
				{
					if (!change.HasConditions)
						continue;

					conditionedChangeCount++;
					foreach (var condition in change.Conditions)
					{
						conditionEntryCount++;
						conditionNameCounts[condition.ConditionName] = conditionNameCounts.GetValueOrDefault(condition.ConditionName) + 1;
					}
				}
			}
		}

		if (conditionEntryCount > 0 && !hasLiveConditionValidatorProvider)
			missingInputs.Add("live Conditions.validate provider");

		var status = DetermineStatus(skillTemplates, conditionEntryCount, hasLiveConditionValidatorProvider);
		return new SkillStatChangeConditionReadinessReport(
			status,
			conditionedChangeCount,
			conditionEntryCount,
			conditionNameCounts
				.OrderBy(pair => pair.Key, StringComparer.Ordinal)
				.Select(pair => new SkillStatChangeConditionNameCount(pair.Key, pair.Value))
				.ToArray(),
			HasLiveConditionValidatorProvider: hasLiveConditionValidatorProvider,
			missingInputs,
			"Change.conditions -> Conditions.validate(Skill/Stat2/Effect); BufEffect.getModifiers attaches conditions to generated stat functions");
	}

	private static IEnumerable<SkillStatChange> EnumerateStatChanges(SkillTemplateSummary template)
	{
		foreach (var effect in template.ArmorMastery)
		foreach (var change in effect.Changes)
			yield return change;

		foreach (var effect in template.WeaponMastery)
		foreach (var change in effect.Changes)
			yield return change;

		foreach (var effect in template.ShieldMastery)
		foreach (var change in effect.Changes)
			yield return change;

		foreach (var effect in template.BuffStatEffects)
		foreach (var change in effect.Changes)
			yield return change;
	}

	private static SkillStatChangeConditionReadinessStatus DetermineStatus(
		SkillTemplateTable? skillTemplates,
		int conditionEntryCount,
		bool hasLiveConditionValidatorProvider)
	{
		if (skillTemplates == null)
			return SkillStatChangeConditionReadinessStatus.MissingSkillTemplates;
		if (conditionEntryCount == 0)
			return SkillStatChangeConditionReadinessStatus.NoConditionMetadata;
		if (!hasLiveConditionValidatorProvider)
			return SkillStatChangeConditionReadinessStatus.BlockedMissingConditionValidators;
		return SkillStatChangeConditionReadinessStatus.Ready;
	}
}

public enum SkillStatChangeConditionReadinessStatus
{
	MissingSkillTemplates,
	NoConditionMetadata,
	BlockedMissingConditionValidators,
	Ready,
}

public sealed record SkillStatChangeConditionReadinessReport(
	SkillStatChangeConditionReadinessStatus Status,
	int ConditionedChangeCount,
	int ConditionEntryCount,
	IReadOnlyList<SkillStatChangeConditionNameCount> ConditionNameCounts,
	bool HasLiveConditionValidatorProvider,
	IReadOnlyList<string> MissingInputs,
	string JavaSource)
{
	public bool IsReadyForConditionedStatChanges => Status == SkillStatChangeConditionReadinessStatus.Ready;
}

public sealed record SkillStatChangeConditionNameCount(string ConditionName, int Count);
