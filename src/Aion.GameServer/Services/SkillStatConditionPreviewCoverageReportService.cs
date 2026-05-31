using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public static class SkillStatConditionPreviewCoverageReportService
{
	public static SkillStatConditionPreviewCoverageReport CreateReport(SkillTemplateTable? skillTemplates)
	{
		if (skillTemplates == null)
		{
			return new SkillStatConditionPreviewCoverageReport(
				SkillStatConditionPreviewCoverageStatus.MissingSkillTemplates,
				ConditionedChangeCount: 0,
				ConditionEntryCount: 0,
				PreviewEvaluableChangeCount: 0,
				BlockedChangeCount: 0,
				[],
				["skill_templates"],
				"BufEffect.getModifiers attaches Change.conditions; pure preview can evaluate only audited Stat2 condition behavior");
		}

		var combinations = new List<SkillStatConditionPreviewCombination>();
		var conditionEntryCount = 0;
		foreach (var template in skillTemplates.Templates)
		{
			foreach (var effect in template.BuffStatEffects)
			foreach (var change in effect.Changes)
			{
				if (!change.HasConditions)
					continue;

				conditionEntryCount += change.Conditions.Count;
				combinations.Add(CreateCombination(template.SkillId, effect.EffectName, change));
			}
		}

		var missingInputs = combinations
			.SelectMany(combination => combination.MissingInputs)
			.Distinct(StringComparer.Ordinal)
			.Order(StringComparer.Ordinal)
			.ToArray();
		var status = DetermineStatus(skillTemplates, combinations);
		return new SkillStatConditionPreviewCoverageReport(
			status,
			combinations.Count,
			conditionEntryCount,
			combinations.Count(combination => combination.Status == SkillStatConditionPreviewCombinationStatus.PreviewEvaluable),
			combinations.Count(combination => combination.Status != SkillStatConditionPreviewCombinationStatus.PreviewEvaluable),
			combinations,
			missingInputs,
			"BufEffect.getModifiers attaches Change.conditions; Conditions.validate(Stat2, IStatFunction) iterates XML/list order and short-circuits false; pure preview coverage is static metadata coverage only, not live CreatureGameStats parity");
	}

	private static SkillStatConditionPreviewCombination CreateCombination(
		int skillId,
		string effectName,
		SkillStatChange change)
	{
		var conditionResults = change.Conditions
			.Select(AnalyzeCondition)
			.ToArray();
		var missingInputs = conditionResults
			.SelectMany(result => result.MissingInputs)
			.Distinct(StringComparer.Ordinal)
			.Order(StringComparer.Ordinal)
			.ToArray();
		var requiredRuntimeInputs = conditionResults
			.SelectMany(result => result.RequiredRuntimeInputs)
			.Distinct(StringComparer.Ordinal)
			.Order(StringComparer.Ordinal)
			.ToArray();
		var status = DetermineCombinationStatus(conditionResults);

		return new SkillStatConditionPreviewCombination(
			status,
			skillId,
			effectName,
			change.Stat,
			change.Func,
			string.Join(" -> ", change.Conditions.Select(condition => condition.ConditionName)),
			conditionResults,
			requiredRuntimeInputs,
			missingInputs);
	}

	private static SkillStatConditionPreviewConditionResult AnalyzeCondition(SkillStatChangeConditionSummary condition)
	{
		if (IsKnownStatPassThroughCondition(condition.ConditionName))
		{
			return new SkillStatConditionPreviewConditionResult(
				condition.ConditionName,
				SkillStatConditionPreviewConditionStatus.PreviewEvaluable,
				[],
				[],
				$"{MapKnownPassThroughJavaType(condition.ConditionName)} inherits Condition.validate(Stat2, IStatFunction) == true");
		}

		return condition.ConditionName switch
		{
			"weapon" => AnalyzeRequiredAttribute(
				condition,
				"weapon",
				["creature condition input snapshot", "player main-hand ItemGroup snapshot"],
				"WeaponCondition.validate(Stat2, IStatFunction) requires XML weapon ItemGroup list plus creature/player equipment input"),
			"charge" => AnalyzeRequiredIntAttribute(
				condition,
				"value",
				["item owner condition input snapshot"],
				"ItemChargeCondition.validate(Stat2, IStatFunction) requires XML value plus IStatFunction Item owner input"),
			"onfly" => new SkillStatConditionPreviewConditionResult(
				condition.ConditionName,
				SkillStatConditionPreviewConditionStatus.PreviewEvaluable,
				["creature condition input snapshot"],
				[],
				"OnFlyCondition.validate(Stat2, IStatFunction) requires stat.getOwner().isFlying() input"),
			_ => new SkillStatConditionPreviewConditionResult(
				condition.ConditionName,
				SkillStatConditionPreviewConditionStatus.UnsupportedCondition,
				[],
				[$"unsupported isolated stat-condition evaluator: {condition.ConditionName}"],
				"Conditions.validate child mapping is not covered by the isolated preview evaluator"),
		};
	}

	private static SkillStatConditionPreviewConditionResult AnalyzeRequiredAttribute(
		SkillStatChangeConditionSummary condition,
		string attributeName,
		IReadOnlyList<string> requiredRuntimeInputs,
		string javaSource)
	{
		if (!condition.Attributes.TryGetValue(attributeName, out var value) || string.IsNullOrWhiteSpace(value))
		{
			return new SkillStatConditionPreviewConditionResult(
				condition.ConditionName,
				SkillStatConditionPreviewConditionStatus.MissingStaticMetadata,
				requiredRuntimeInputs,
				[$"XML {attributeName} attribute"],
				javaSource);
		}

		return new SkillStatConditionPreviewConditionResult(
			condition.ConditionName,
			SkillStatConditionPreviewConditionStatus.PreviewEvaluable,
			requiredRuntimeInputs,
			[],
			javaSource);
	}

	private static SkillStatConditionPreviewConditionResult AnalyzeRequiredIntAttribute(
		SkillStatChangeConditionSummary condition,
		string attributeName,
		IReadOnlyList<string> requiredRuntimeInputs,
		string javaSource)
	{
		if (!condition.Attributes.TryGetValue(attributeName, out var value) || !int.TryParse(value, out _))
		{
			return new SkillStatConditionPreviewConditionResult(
				condition.ConditionName,
				SkillStatConditionPreviewConditionStatus.MissingStaticMetadata,
				requiredRuntimeInputs,
				[$"XML {attributeName} attribute integer"],
				javaSource);
		}

		return new SkillStatConditionPreviewConditionResult(
			condition.ConditionName,
			SkillStatConditionPreviewConditionStatus.PreviewEvaluable,
			requiredRuntimeInputs,
			[],
			javaSource);
	}

	private static SkillStatConditionPreviewCombinationStatus DetermineCombinationStatus(
		IReadOnlyList<SkillStatConditionPreviewConditionResult> conditionResults)
	{
		if (conditionResults.Any(result => result.Status == SkillStatConditionPreviewConditionStatus.UnsupportedCondition))
			return SkillStatConditionPreviewCombinationStatus.BlockedUnsupportedCondition;
		if (conditionResults.Any(result => result.Status == SkillStatConditionPreviewConditionStatus.MissingStaticMetadata))
			return SkillStatConditionPreviewCombinationStatus.BlockedStaticMetadata;
		return SkillStatConditionPreviewCombinationStatus.PreviewEvaluable;
	}

	private static SkillStatConditionPreviewCoverageStatus DetermineStatus(
		SkillTemplateTable skillTemplates,
		IReadOnlyList<SkillStatConditionPreviewCombination> combinations)
	{
		if (skillTemplates.Templates.Count == 0 || combinations.Count == 0)
			return SkillStatConditionPreviewCoverageStatus.NoConditionedChanges;
		if (combinations.Any(combination => combination.Status == SkillStatConditionPreviewCombinationStatus.BlockedUnsupportedCondition))
			return SkillStatConditionPreviewCoverageStatus.BlockedUnsupportedConditions;
		if (combinations.Any(combination => combination.Status == SkillStatConditionPreviewCombinationStatus.BlockedStaticMetadata))
			return SkillStatConditionPreviewCoverageStatus.BlockedStaticMetadata;
		return SkillStatConditionPreviewCoverageStatus.PreviewEvaluable;
	}

	private static bool IsKnownStatPassThroughCondition(string conditionName)
	{
		return conditionName is
			"abnormal" or
			"back" or
			"chain" or
			"chargearmor" or
			"chargeweapon" or
			"combatcheck" or
			"dp" or
			"form" or
			"front" or
			"hp" or
			"lefthandweapon" or
			"move_casting" or
			"mp" or
			"noflying" or
			"polishchargeweapon" or
			"race" or
			"ride_robot" or
			"selfflying" or
			"skillcharge" or
			"target" or
			"targetflying";
	}

	private static string MapKnownPassThroughJavaType(string conditionName)
	{
		return conditionName switch
		{
			"abnormal" => "AbnormalStateCondition",
			"back" => "BackCondition",
			"chain" => "ChainCondition",
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
			"polishchargeweapon" => "PolishChargeCondition",
			"race" => "RaceCondition",
			"ride_robot" => "RideRobotCondition",
			"selfflying" => "SelfFlyingCondition",
			"skillcharge" => "SkillChargeCondition",
			"target" => "TargetCondition",
			"targetflying" => "TargetFlyingCondition",
			_ => "Condition",
		};
	}
}

public enum SkillStatConditionPreviewCoverageStatus
{
	MissingSkillTemplates,
	NoConditionedChanges,
	BlockedUnsupportedConditions,
	BlockedStaticMetadata,
	PreviewEvaluable,
}

public enum SkillStatConditionPreviewCombinationStatus
{
	PreviewEvaluable,
	BlockedUnsupportedCondition,
	BlockedStaticMetadata,
}

public enum SkillStatConditionPreviewConditionStatus
{
	PreviewEvaluable,
	UnsupportedCondition,
	MissingStaticMetadata,
}

public sealed record SkillStatConditionPreviewCoverageReport(
	SkillStatConditionPreviewCoverageStatus Status,
	int ConditionedChangeCount,
	int ConditionEntryCount,
	int PreviewEvaluableChangeCount,
	int BlockedChangeCount,
	IReadOnlyList<SkillStatConditionPreviewCombination> Combinations,
	IReadOnlyList<string> MissingInputs,
	string JavaSource)
{
	public bool HasCompleteStaticPreviewCoverage => Status == SkillStatConditionPreviewCoverageStatus.PreviewEvaluable;
}

public sealed record SkillStatConditionPreviewCombination(
	SkillStatConditionPreviewCombinationStatus Status,
	int SkillId,
	string EffectName,
	string Stat,
	string Func,
	string ConditionSequence,
	IReadOnlyList<SkillStatConditionPreviewConditionResult> Conditions,
	IReadOnlyList<string> RequiredRuntimeInputs,
	IReadOnlyList<string> MissingInputs);

public sealed record SkillStatConditionPreviewConditionResult(
	string ConditionName,
	SkillStatConditionPreviewConditionStatus Status,
	IReadOnlyList<string> RequiredRuntimeInputs,
	IReadOnlyList<string> MissingInputs,
	string JavaSource);
