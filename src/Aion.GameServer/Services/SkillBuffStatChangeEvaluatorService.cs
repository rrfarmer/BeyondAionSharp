using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public static class SkillBuffStatChangeEvaluatorService
{
	public static SkillBuffStatChangeEvaluation Evaluate(
		string statName,
		float baseValue,
		IReadOnlyList<SkillStatChange> changes,
		int skillLevel)
	{
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
				change.Conditions))
			.OrderBy(step => step.Priority)
			.ToArray();

		if (applicableChanges.Length == 0)
		{
			return new SkillBuffStatChangeEvaluation(
				SkillBuffStatChangeEvaluationStatus.NoApplicableChanges,
				statName,
				baseValue,
				baseValue,
				0,
				(int)baseValue,
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
				0,
				(int)baseValue,
				applicableChanges,
				"BufEffect.getModifiers supports ADD, PERCENT, and REPLACE for this evaluator slice");
		}

		if (applicableChanges.Any(step => step.HasConditions))
		{
			return new SkillBuffStatChangeEvaluation(
				SkillBuffStatChangeEvaluationStatus.UnsupportedConditions,
				statName,
				baseValue,
				baseValue,
				0,
				(int)baseValue,
				applicableChanges,
				"BufEffect.getModifiers attaches Change.conditions to stat functions; this pure evaluator does not evaluate Conditions.validate");
		}

		var currentBase = baseValue;
		var bonus = 0f;
		foreach (var step in applicableChanges)
		{
			switch (step.Func)
			{
				case "REPLACE":
					currentBase = step.EffectiveValue;
					break;
				case "PERCENT":
					bonus += (int)currentBase * step.EffectiveValue / 100f;
					break;
				case "ADD":
					bonus += step.EffectiveValue;
					break;
			}
		}

		return new SkillBuffStatChangeEvaluation(
			SkillBuffStatChangeEvaluationStatus.Evaluated,
			statName,
			baseValue,
			currentBase,
			bonus,
			(int)(currentBase + bonus),
			applicableChanges,
			"BufEffect.getModifiers -> StatSetFunction priority 40, StatRateFunction bonus priority 50, StatAddFunction bonus priority 60, AdditionStat.getCurrent truncates to int");
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
	IReadOnlyList<SkillStatChangeConditionSummary> Conditions)
{
	public bool HasConditions => Conditions.Count > 0;
}
