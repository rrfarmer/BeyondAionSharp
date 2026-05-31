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

		var validatorPlans = conditionNameCounts
			.OrderBy(pair => pair.Key, StringComparer.Ordinal)
			.Select(pair => CreateValidatorPlan(pair.Key, pair.Value, hasLiveConditionValidatorProvider))
			.ToArray();

		if (validatorPlans.Any(plan => !plan.HasJavaConditionMapping))
			missingInputs.Add("supported Java Conditions child mapping");
		if (conditionEntryCount > 0 && !hasLiveConditionValidatorProvider)
			missingInputs.Add("live Conditions.validate provider");

		var status = DetermineStatus(
			skillTemplates,
			conditionEntryCount,
			validatorPlans.Any(plan => !plan.HasJavaConditionMapping),
			hasLiveConditionValidatorProvider);
		return new SkillStatChangeConditionReadinessReport(
			status,
			conditionedChangeCount,
			conditionEntryCount,
			conditionNameCounts
				.OrderBy(pair => pair.Key, StringComparer.Ordinal)
				.Select(pair => new SkillStatChangeConditionNameCount(pair.Key, pair.Value))
				.ToArray(),
			validatorPlans,
			HasLiveConditionValidatorProvider: hasLiveConditionValidatorProvider,
			"CreatureGameStats.getStat calls IStatFunction.validate(stat) before IStatFunction.apply(stat)",
			"Conditions.validate iterates child Condition instances in XML/list order and returns false on the first failed child validator",
			"Conditioned stat functions must be skipped without applying when StatFunction.validate returns false",
			missingInputs,
			"Change.conditions -> StatFunction.validate -> Conditions.validate(Stat2, IStatFunction) before apply; BufEffect.getModifiers attaches conditions to generated stat functions");
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
		bool hasUnsupportedConditionMetadata,
		bool hasLiveConditionValidatorProvider)
	{
		if (skillTemplates == null)
			return SkillStatChangeConditionReadinessStatus.MissingSkillTemplates;
		if (conditionEntryCount == 0)
			return SkillStatChangeConditionReadinessStatus.NoConditionMetadata;
		if (hasUnsupportedConditionMetadata)
			return SkillStatChangeConditionReadinessStatus.UnsupportedConditionMetadata;
		if (!hasLiveConditionValidatorProvider)
			return SkillStatChangeConditionReadinessStatus.BlockedMissingConditionValidators;
		return SkillStatChangeConditionReadinessStatus.Ready;
	}

	private static SkillStatChangeConditionValidatorPlan CreateValidatorPlan(
		string conditionName,
		int entryCount,
		bool hasLiveConditionValidatorProvider)
	{
		var javaConditionType = MapJavaConditionType(conditionName);
		var hasMapping = !string.IsNullOrEmpty(javaConditionType);
		var missingInputs = new List<string>();

		if (!hasMapping)
			missingInputs.Add("supported Java Conditions child mapping");
		if (hasMapping && !hasLiveConditionValidatorProvider)
			missingInputs.Add("live Conditions.validate provider");

		var status = !hasMapping
			? SkillStatChangeConditionValidatorPlanStatus.UnsupportedConditionMetadata
			: hasLiveConditionValidatorProvider
				? SkillStatChangeConditionValidatorPlanStatus.Ready
				: SkillStatChangeConditionValidatorPlanStatus.BlockedMissingConditionValidatorProvider;

		return new SkillStatChangeConditionValidatorPlan(
			status,
			conditionName,
			javaConditionType ?? "unsupported",
			entryCount,
			hasMapping,
			hasLiveConditionValidatorProvider,
			"StatFunction.validate delegates to Conditions.validate(stat, statFunction) before apply",
			"Conditions.validate returns false on first child Condition failure and skips remaining validation",
			missingInputs,
			"Conditions.validate iterates child Condition instances and returns false on the first child validator failure");
	}

	private static string? MapJavaConditionType(string conditionName)
	{
		return conditionName switch
		{
			"abnormal" => "AbnormalStateCondition",
			"back" => "BackCondition",
			"chain" => "ChainCondition",
			"charge" => "ItemChargeCondition",
			"chargearmor" => "ChargeArmorCondition",
			"chargeweapon" => "ChargeWeaponCondition",
			"combatcheck" => "CombatCheckCondition",
			"dp" => "DpCondition",
			"form" => "FormCondition",
			"front" => "FrontCondition",
			"hp" => "HpCondition",
			"lefthandweapon" => "LeftHandCondition",
			"move_casting" => "PlayerMovedCondition",
			"mp" => "MpCondition",
			"noflying" => "NoFlyingCondition",
			"onfly" => "OnFlyCondition",
			"polishchargeweapon" => "PolishChargeCondition",
			"race" => "RaceCondition",
			"ride_robot" => "RideRobotCondition",
			"selfflying" => "SelfFlyingCondition",
			"skillcharge" => "SkillChargeCondition",
			"target" => "TargetCondition",
			"targetflying" => "TargetFlyingCondition",
			"weapon" => "WeaponCondition",
			_ => null,
		};
	}
}

public enum SkillStatChangeConditionReadinessStatus
{
	MissingSkillTemplates,
	NoConditionMetadata,
	UnsupportedConditionMetadata,
	BlockedMissingConditionValidators,
	Ready,
}

public enum SkillStatChangeConditionValidatorPlanStatus
{
	UnsupportedConditionMetadata,
	BlockedMissingConditionValidatorProvider,
	Ready,
}

public sealed record SkillStatChangeConditionReadinessReport(
	SkillStatChangeConditionReadinessStatus Status,
	int ConditionedChangeCount,
	int ConditionEntryCount,
	IReadOnlyList<SkillStatChangeConditionNameCount> ConditionNameCounts,
	IReadOnlyList<SkillStatChangeConditionValidatorPlan> ValidatorPlans,
	bool HasLiveConditionValidatorProvider,
	string ValidateBeforeApplyRule,
	string ConditionShortCircuitRule,
	string FailedValidationApplyRule,
	IReadOnlyList<string> MissingInputs,
	string JavaSource)
{
	public bool IsReadyForConditionedStatChanges => Status == SkillStatChangeConditionReadinessStatus.Ready;
}

public sealed record SkillStatChangeConditionNameCount(string ConditionName, int Count);

public sealed record SkillStatChangeConditionValidatorPlan(
	SkillStatChangeConditionValidatorPlanStatus Status,
	string ConditionName,
	string JavaConditionType,
	int EntryCount,
	bool HasJavaConditionMapping,
	bool HasLiveConditionValidatorProvider,
	string ValidateBeforeApplyRule,
	string ConditionShortCircuitRule,
	IReadOnlyList<string> MissingInputs,
	string JavaSource)
{
	public bool IsReadyForValidation => Status == SkillStatChangeConditionValidatorPlanStatus.Ready;
}
