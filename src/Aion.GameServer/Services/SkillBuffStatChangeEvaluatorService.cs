using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public static class SkillBuffStatChangeEvaluatorService
{
	public static SkillBuffStatChangeEvaluation Evaluate(
		string statName,
		float baseValue,
		IReadOnlyList<SkillStatChange> changes,
		int skillLevel,
		float initialBonus = 0f,
		SkillBuffStatConditionEvaluationContext? conditionContext = null)
	{
		var initialState = SkillBuffStatFormulaService.CreateState(baseValue, initialBonus);
		var applicableChanges = changes
			.Where(change => string.Equals(change.Stat, statName, StringComparison.Ordinal))
			.Select(change => new SkillBuffStatChangeStep(
				change.Stat,
				change.Func,
				change.Value,
				change.Delta,
				change.Value + change.Delta * skillLevel,
				GetPriority(change.Func),
				IsSupportedFunc(change.Func),
				change.Conditions,
				[]))
			.OrderBy(step => step.Priority)
			.ToArray();

		if (applicableChanges.Length == 0)
		{
			return new SkillBuffStatChangeEvaluation(
				SkillBuffStatChangeEvaluationStatus.NoApplicableChanges,
				statName,
				baseValue,
				baseValue,
				initialBonus,
				SkillBuffStatFormulaService.GetCurrent(initialState),
				applicableChanges,
				"BufEffect.getModifiers -> no matching Change.stat for requested StatEnum");
		}

		if (applicableChanges.Any(step => !step.IsSupported))
		{
			return new SkillBuffStatChangeEvaluation(
				SkillBuffStatChangeEvaluationStatus.UnsupportedFunction,
				statName,
				baseValue,
				baseValue,
				initialBonus,
				SkillBuffStatFormulaService.GetCurrent(initialState),
				applicableChanges,
				"BufEffect.getModifiers supports ADD, PERCENT, and REPLACE for this evaluator slice");
		}

		if (applicableChanges.Any(step => step.HasConditions))
		{
			if (conditionContext == null)
			{
				return new SkillBuffStatChangeEvaluation(
					SkillBuffStatChangeEvaluationStatus.UnsupportedConditions,
					statName,
					baseValue,
					baseValue,
					initialBonus,
					SkillBuffStatFormulaService.GetCurrent(initialState),
					applicableChanges,
					"BufEffect.getModifiers attaches Change.conditions to stat functions; pass SkillBuffStatConditionEvaluationContext to use isolated Conditions.validate preview evaluation");
			}

			var conditionedPreview = EvaluateWithConditions(
				statName,
				baseValue,
				initialBonus,
				initialState,
				applicableChanges,
				conditionContext);
			if (conditionedPreview != null)
				return conditionedPreview;
		}

		var state = initialState;
		var appliedStepCount = 0;
		foreach (var step in applicableChanges)
		{
			if (step.HasConditions && step.ConditionResults.Any(result => result.Status == SkillStatConditionEvaluationStatus.NotSatisfied))
				continue;

			state = step.Func switch
			{
				"REPLACE" => SkillBuffStatFormulaService.ApplySet(state, step.EffectiveValue, isBonus: false),
				"PERCENT" => SkillBuffStatFormulaService.ApplyRate(
					state,
					SkillBuffStatFormulaMode.Addition,
					statName,
					step.EffectiveValue,
					isBonus: true),
				"ADD" => SkillBuffStatFormulaService.ApplyAdd(
					state,
					SkillBuffStatFormulaMode.Addition,
					step.EffectiveValue,
					isBonus: true),
				_ => state,
			};
			appliedStepCount++;
		}

		return new SkillBuffStatChangeEvaluation(
			appliedStepCount == 0 && applicableChanges.Any(step => step.HasConditions)
				? SkillBuffStatChangeEvaluationStatus.ConditionNotSatisfied
				: SkillBuffStatChangeEvaluationStatus.Evaluated,
			statName,
			baseValue,
			state.Base,
			state.Bonus,
			SkillBuffStatFormulaService.GetCurrent(state),
			applicableChanges,
			"BufEffect.getModifiers -> StatSetFunction priority 40, StatRateFunction bonus priority 50, StatAddFunction bonus priority 60; CreatureGameStats.getStat calls validate before apply; SkillBuffStatFormulaService mirrors AdditionStat/Stat2 math and Java negative SPEED rate handling for this isolated evaluator");
	}

	private static SkillBuffStatChangeEvaluation? EvaluateWithConditions(
		string statName,
		float baseValue,
		float initialBonus,
		SkillBuffStatFormulaState initialState,
		SkillBuffStatChangeStep[] applicableChanges,
		SkillBuffStatConditionEvaluationContext conditionContext)
	{
		for (var index = 0; index < applicableChanges.Length; index++)
		{
			var step = applicableChanges[index];
			if (!step.HasConditions)
				continue;

			var conditionResults = new List<SkillStatConditionEvaluationResult>();
			foreach (var condition in step.Conditions)
			{
				var result = SkillStatConditionEvaluatorService.Evaluate(
					condition,
					conditionContext.CreatureSnapshot,
					conditionContext.ItemOwnerSnapshot);
				conditionResults.Add(result);
				if (result.Status != SkillStatConditionEvaluationStatus.Satisfied)
					break;
			}

			applicableChanges[index] = step with { ConditionResults = conditionResults };
			var blockingResult = conditionResults.LastOrDefault(result => result.Status != SkillStatConditionEvaluationStatus.Satisfied);
			if (blockingResult == null || blockingResult.Status == SkillStatConditionEvaluationStatus.NotSatisfied)
				continue;

			return new SkillBuffStatChangeEvaluation(
				blockingResult.Status == SkillStatConditionEvaluationStatus.MissingInput
					? SkillBuffStatChangeEvaluationStatus.ConditionMissingInput
					: SkillBuffStatChangeEvaluationStatus.UnsupportedConditions,
				statName,
				baseValue,
				baseValue,
				initialBonus,
				SkillBuffStatFormulaService.GetCurrent(initialState),
				applicableChanges,
				"Conditions.validate iterates child Condition instances in XML/list order and returns false on the first failed child; this pure evaluator stops previewing when a required condition input is missing or unsupported");
		}

		return null;
	}

	private static int GetPriority(string func)
	{
		return func switch
		{
			"REPLACE" => 40,
			"PERCENT" => 50,
			"ADD" => 60,
			_ => int.MaxValue,
		};
	}

	private static bool IsSupportedFunc(string func)
	{
		return func is "ADD" or "PERCENT" or "REPLACE";
	}
}

public enum SkillBuffStatChangeEvaluationStatus
{
	Evaluated,
	NoApplicableChanges,
	UnsupportedFunction,
	UnsupportedConditions,
	ConditionMissingInput,
	ConditionNotSatisfied,
}

public sealed record SkillBuffStatChangeEvaluation(
	SkillBuffStatChangeEvaluationStatus Status,
	string StatName,
	float OriginalBase,
	float FinalBase,
	float Bonus,
	int Current,
	IReadOnlyList<SkillBuffStatChangeStep> Steps,
	string JavaSource);

public sealed record SkillBuffStatChangeStep(
	string Stat,
	string Func,
	int Value,
	int Delta,
	int EffectiveValue,
	int Priority,
	bool IsSupported,
	IReadOnlyList<SkillStatChangeConditionSummary> Conditions,
	IReadOnlyList<SkillStatConditionEvaluationResult> ConditionResults)
{
	public bool HasConditions => Conditions.Count > 0;
}

public sealed record SkillBuffStatConditionEvaluationContext(
	SkillStatConditionCreatureInputSnapshot? CreatureSnapshot = null,
	SkillStatConditionItemOwnerInputSnapshot? ItemOwnerSnapshot = null);
