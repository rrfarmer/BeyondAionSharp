using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SkillBuffStat2EvaluationReadinessReportServiceTests
{
	[Fact]
	public void CreateReport_RecordsNoFunctionPlansAndJavaFormula()
	{
		var report = SkillBuffStat2EvaluationReadinessReportService.CreateReport([]);

		Assert.Equal(SkillBuffStat2EvaluationReadinessStatus.NoFunctionPlans, report.Status);
		Assert.False(report.IsReadyForRuntimeEvaluation);
		Assert.Equal(0, report.FunctionPlanCount);
		Assert.Equal(0, report.FunctionCount);
		Assert.Empty(report.StatNames);
		Assert.Equal("(int) (base * baseRate + bonus * bonusRate + base * fixedBonusRate)", report.CurrentValueFormula);
		Assert.Contains("Stat2.getCurrent", report.JavaSource, StringComparison.Ordinal);
		Assert.Contains("live Stat2 base/bonus/baseRate/bonusRate/fixedBonusRate state provider", report.MissingInputs);
		Assert.Contains("live Stat2.getCurrent/getExactCurrent formula provider", report.MissingInputs);
		Assert.Contains("live AdditionStat addToBase/addToBonus/calculatePercent provider", report.MissingInputs);
		Assert.Contains("live ReverseStat addToBase/addToBonus/calculatePercent provider", report.MissingInputs);
		Assert.Contains("live StatAddFunction/StatRateFunction/StatSetFunction apply provider", report.MissingInputs);
		Assert.Contains("live StatCapUtil.calculateBaseValue provider", report.MissingInputs);
	}

	[Fact]
	public void CreateReport_CapturesFunctionAndStat2Evidence()
	{
		var conditioned = new SkillStatChange("DR_BOOST", "ADD", 7, 0);
		conditioned.AddCondition(new SkillStatChangeConditionSummary(
			"weapon",
			new Dictionary<string, string>(StringComparer.Ordinal) { ["weapon"] = "ORB" }));

		var report = SkillBuffStat2EvaluationReadinessReportService.CreateReport(
		[
			CreatePlan(
				9878,
				"drboost",
				[
					conditioned,
					new SkillStatChange("DR_BOOST", "REPLACE", 80, 0),
					new SkillStatChange("BOOST_DROP_RATE", "PERCENT", 50, 0)
				])
		]);

		Assert.Equal(SkillBuffStat2EvaluationReadinessStatus.BlockedMissingStat2StateProvider, report.Status);
		Assert.Equal(1, report.FunctionPlanCount);
		Assert.Equal(3, report.FunctionCount);
		Assert.Equal(["BOOST_DROP_RATE", "DR_BOOST"], report.StatNames);
		Assert.Equal(1, report.AddFunctionCount);
		Assert.Equal(1, report.RateFunctionCount);
		Assert.Equal(1, report.SetFunctionCount);
		Assert.Equal(2, report.BonusFunctionCount);
		Assert.Equal(1, report.BaseFunctionCount);
		Assert.Equal(1, report.ConditionedFunctionCount);
	}

	[Fact]
	public void CreateReport_BlocksUnsupportedFunctionPlansBeforeLiveProviders()
	{
		var report = SkillBuffStat2EvaluationReadinessReportService.CreateReport(
		[
			CreatePlan(
				8472,
				"boostdroprate",
				[new SkillStatChange("BOOST_DROP_RATE", "ABS", 20, 0)],
				hasOwner: true,
				hasRegistry: true,
				hasConditionValidator: true)
		],
			hasLiveStat2StateProvider: true,
			hasLiveCurrentValueFormulaProvider: true,
			hasLiveAdditionStatProvider: true,
			hasLiveReverseStatProvider: true,
			hasLiveStatFunctionApplyProvider: true,
			hasLiveStatCapProvider: true);

		Assert.Equal(SkillBuffStat2EvaluationReadinessStatus.UnsupportedFunctionPlan, report.Status);
		Assert.False(report.IsReadyForRuntimeEvaluation);
		Assert.Contains("supported BufEffect stat function mapping", report.MissingInputs);
	}

	[Fact]
	public void CreateReport_RequiresEveryLiveRuntimeEvaluationGate()
	{
		var plans = new[]
		{
			CreatePlan(
				8472,
				"boostdroprate",
				[new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0)],
				hasOwner: true,
				hasRegistry: true,
				hasConditionValidator: true)
		};

		var missingFormula = SkillBuffStat2EvaluationReadinessReportService.CreateReport(
			plans,
			hasLiveStat2StateProvider: true);
		Assert.Equal(SkillBuffStat2EvaluationReadinessStatus.BlockedMissingCurrentValueFormulaProvider, missingFormula.Status);

		var missingAddition = SkillBuffStat2EvaluationReadinessReportService.CreateReport(
			plans,
			hasLiveStat2StateProvider: true,
			hasLiveCurrentValueFormulaProvider: true);
		Assert.Equal(SkillBuffStat2EvaluationReadinessStatus.BlockedMissingAdditionStatProvider, missingAddition.Status);

		var missingReverse = SkillBuffStat2EvaluationReadinessReportService.CreateReport(
			plans,
			hasLiveStat2StateProvider: true,
			hasLiveCurrentValueFormulaProvider: true,
			hasLiveAdditionStatProvider: true);
		Assert.Equal(SkillBuffStat2EvaluationReadinessStatus.BlockedMissingReverseStatProvider, missingReverse.Status);

		var missingApply = SkillBuffStat2EvaluationReadinessReportService.CreateReport(
			plans,
			hasLiveStat2StateProvider: true,
			hasLiveCurrentValueFormulaProvider: true,
			hasLiveAdditionStatProvider: true,
			hasLiveReverseStatProvider: true);
		Assert.Equal(SkillBuffStat2EvaluationReadinessStatus.BlockedMissingStatFunctionApplyProvider, missingApply.Status);

		var missingCap = SkillBuffStat2EvaluationReadinessReportService.CreateReport(
			plans,
			hasLiveStat2StateProvider: true,
			hasLiveCurrentValueFormulaProvider: true,
			hasLiveAdditionStatProvider: true,
			hasLiveReverseStatProvider: true,
			hasLiveStatFunctionApplyProvider: true);
		Assert.Equal(SkillBuffStat2EvaluationReadinessStatus.BlockedMissingStatCapProvider, missingCap.Status);
	}

	[Fact]
	public void CreateReport_IsReadyOnlyWhenEveryRuntimeEvaluationGateExists()
	{
		var report = SkillBuffStat2EvaluationReadinessReportService.CreateReport(
		[
			CreatePlan(
				8472,
				"boostdroprate",
				[new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0)],
				hasOwner: true,
				hasRegistry: true,
				hasConditionValidator: true)
		],
			hasLiveStat2StateProvider: true,
			hasLiveCurrentValueFormulaProvider: true,
			hasLiveAdditionStatProvider: true,
			hasLiveReverseStatProvider: true,
			hasLiveStatFunctionApplyProvider: true,
			hasLiveStatCapProvider: true);

		Assert.Equal(SkillBuffStat2EvaluationReadinessStatus.Ready, report.Status);
		Assert.True(report.IsReadyForRuntimeEvaluation);
		Assert.Empty(report.MissingInputs);
	}

	private static SkillBuffStatFunctionRegistryPlan CreatePlan(
		int skillId,
		string effectName,
		IReadOnlyList<SkillStatChange> changes,
		bool hasOwner = false,
		bool hasRegistry = false,
		bool hasConditionValidator = false)
	{
		return SkillBuffStatFunctionPlanService.CreateRegistryPlan(
			skillId,
			effectName,
			1,
			changes,
			hasOwner,
			hasRegistry,
			hasConditionValidator);
	}
}
