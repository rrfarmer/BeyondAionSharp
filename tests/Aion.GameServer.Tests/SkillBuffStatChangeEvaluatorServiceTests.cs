using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SkillBuffStatChangeEvaluatorServiceTests
{
	[Fact]
	public void Evaluate_AddsDeltaAdjustedAddChangeLikeJavaBufEffect()
	{
		var evaluation = SkillBuffStatChangeEvaluatorService.Evaluate(
			"BOOST_DROP_RATE",
			100,
			[new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 5)],
			skillLevel: 3);

		Assert.Equal(SkillBuffStatChangeEvaluationStatus.Evaluated, evaluation.Status);
		Assert.Equal(100, evaluation.OriginalBase);
		Assert.Equal(100, evaluation.FinalBase);
		Assert.Equal(35, evaluation.Bonus);
		Assert.Equal(135, evaluation.Current);
		var step = Assert.Single(evaluation.Steps);
		Assert.Equal(35, step.EffectiveValue);
		Assert.Equal(60, step.Priority);
		Assert.Contains("BufEffect.getModifiers", evaluation.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Evaluate_AppliesReplaceBeforePercentBeforeAddLikeJavaPriorities()
	{
		var evaluation = SkillBuffStatChangeEvaluatorService.Evaluate(
			"DR_BOOST",
			100,
			[
				new SkillStatChange("DR_BOOST", "ADD", 7, 0),
				new SkillStatChange("DR_BOOST", "PERCENT", 50, 0),
				new SkillStatChange("DR_BOOST", "REPLACE", 80, 0)
			],
			skillLevel: 1);

		Assert.Equal(SkillBuffStatChangeEvaluationStatus.Evaluated, evaluation.Status);
		Assert.Equal(80, evaluation.FinalBase);
		Assert.Equal(47, evaluation.Bonus);
		Assert.Equal(127, evaluation.Current);
		Assert.Equal(["REPLACE", "PERCENT", "ADD"], evaluation.Steps.Select(step => step.Func).ToArray());
		Assert.Equal([40, 50, 60], evaluation.Steps.Select(step => step.Priority).ToArray());
	}

	[Fact]
	public void Evaluate_TruncatesCurrentLikeJavaStat2()
	{
		var evaluation = SkillBuffStatChangeEvaluatorService.Evaluate(
			"BOOST_DROP_RATE",
			99.9f,
			[new SkillStatChange("BOOST_DROP_RATE", "PERCENT", 10, 0)],
			skillLevel: 1);

		Assert.Equal(SkillBuffStatChangeEvaluationStatus.Evaluated, evaluation.Status);
		Assert.Equal(9.9f, evaluation.Bonus, precision: 3);
		Assert.Equal(109, evaluation.Current);
	}

	[Fact]
	public void Evaluate_ReportsNoApplicableChanges()
	{
		var evaluation = SkillBuffStatChangeEvaluatorService.Evaluate(
			"BOOST_DROP_RATE",
			100,
			[new SkillStatChange("DR_BOOST", "ADD", 20, 0)],
			skillLevel: 1);

		Assert.Equal(SkillBuffStatChangeEvaluationStatus.NoApplicableChanges, evaluation.Status);
		Assert.Equal(100, evaluation.Current);
		Assert.Empty(evaluation.Steps);
	}

	[Fact]
	public void Evaluate_ReportsUnsupportedFunctionWithoutApplyingChanges()
	{
		var evaluation = SkillBuffStatChangeEvaluatorService.Evaluate(
			"BOOST_DROP_RATE",
			100,
			[new SkillStatChange("BOOST_DROP_RATE", "ABS", 20, 0)],
			skillLevel: 1);

		Assert.Equal(SkillBuffStatChangeEvaluationStatus.UnsupportedFunction, evaluation.Status);
		Assert.Equal(100, evaluation.Current);
		var step = Assert.Single(evaluation.Steps);
		Assert.False(step.IsSupported);
		Assert.Equal(int.MaxValue, step.Priority);
	}

	[Fact]
	public void Evaluate_ReportsUnsupportedConditionsWithoutApplyingChanges()
	{
		var conditionedChange = new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0);
		conditionedChange.AddCondition(new SkillStatChangeConditionSummary(
			"weapon",
			new Dictionary<string, string>(StringComparer.Ordinal) { ["weapon"] = "ORB" }));

		var evaluation = SkillBuffStatChangeEvaluatorService.Evaluate(
			"BOOST_DROP_RATE",
			100,
			[conditionedChange],
			skillLevel: 1);

		Assert.Equal(SkillBuffStatChangeEvaluationStatus.UnsupportedConditions, evaluation.Status);
		Assert.Equal(100, evaluation.Current);
		var step = Assert.Single(evaluation.Steps);
		Assert.True(step.HasConditions);
		Assert.Equal("weapon", Assert.Single(step.Conditions).ConditionName);
	}
}
