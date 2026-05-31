namespace Aion.GameServer.Services;

public static class SkillBuffStat2EvaluationReadinessReportService
{
	public const string JavaCurrentValueFormula = "(int) (base * baseRate + bonus * bonusRate + base * fixedBonusRate)";

	public static SkillBuffStat2EvaluationReadinessReport CreateReport(
		IReadOnlyList<SkillBuffStatFunctionRegistryPlan> functionPlans,
		bool hasLiveStat2StateProvider = false,
		bool hasLiveCurrentValueFormulaProvider = false,
		bool hasLiveAdditionStatProvider = false,
		bool hasLiveReverseStatProvider = false,
		bool hasLiveStatFunctionApplyProvider = false,
		bool hasLiveStatCapProvider = false)
	{
		var functions = functionPlans
			.SelectMany(plan => plan.Functions)
			.ToArray();
		var missingInputs = new List<string>();

		if (!hasLiveStat2StateProvider)
			missingInputs.Add("live Stat2 base/bonus/baseRate/bonusRate/fixedBonusRate state provider");
		if (!hasLiveCurrentValueFormulaProvider)
			missingInputs.Add("live Stat2.getCurrent/getExactCurrent formula provider");
		if (!hasLiveAdditionStatProvider)
			missingInputs.Add("live AdditionStat addToBase/addToBonus/calculatePercent provider");
		if (!hasLiveReverseStatProvider)
			missingInputs.Add("live ReverseStat addToBase/addToBonus/calculatePercent provider");
		if (!hasLiveStatFunctionApplyProvider)
			missingInputs.Add("live StatAddFunction/StatRateFunction/StatSetFunction apply provider");
		if (!hasLiveStatCapProvider)
			missingInputs.Add("live StatCapUtil.calculateBaseValue provider");
		if (functionPlans.Any(plan => plan.Status == SkillBuffStatFunctionRegistryPlanStatus.UnsupportedFunction))
			missingInputs.Add("supported BufEffect stat function mapping");

		var status = DetermineStatus(
			functionPlans,
			functions.Length,
			hasLiveStat2StateProvider,
			hasLiveCurrentValueFormulaProvider,
			hasLiveAdditionStatProvider,
			hasLiveReverseStatProvider,
			hasLiveStatFunctionApplyProvider,
			hasLiveStatCapProvider);
		return new SkillBuffStat2EvaluationReadinessReport(
			status,
			functionPlans.Count,
			functions.Length,
			functions.Select(function => function.StatName)
				.Distinct(StringComparer.Ordinal)
				.Order(StringComparer.Ordinal)
				.ToArray(),
			functions.Count(function => function.JavaFunctionType == "StatAddFunction"),
			functions.Count(function => function.JavaFunctionType == "StatRateFunction"),
			functions.Count(function => function.JavaFunctionType == "StatSetFunction"),
			functions.Count(function => function.IsBonus),
			functions.Count(function => !function.IsBonus),
			functions.Count(function => function.HasConditions),
			hasLiveStat2StateProvider,
			hasLiveCurrentValueFormulaProvider,
			hasLiveAdditionStatProvider,
			hasLiveReverseStatProvider,
			hasLiveStatFunctionApplyProvider,
			hasLiveStatCapProvider,
			JavaCurrentValueFormula,
			missingInputs,
			"CreatureGameStats.getStat creates AdditionStat or ReverseStat, applies sorted IStatFunction instances that validate(stat), then calls StatCapUtil.calculateBaseValue; Stat2.getCurrent truncates base * baseRate + bonus * bonusRate + base * fixedBonusRate");
	}

	private static SkillBuffStat2EvaluationReadinessStatus DetermineStatus(
		IReadOnlyList<SkillBuffStatFunctionRegistryPlan> functionPlans,
		int functionCount,
		bool hasLiveStat2StateProvider,
		bool hasLiveCurrentValueFormulaProvider,
		bool hasLiveAdditionStatProvider,
		bool hasLiveReverseStatProvider,
		bool hasLiveStatFunctionApplyProvider,
		bool hasLiveStatCapProvider)
	{
		if (functionPlans.Count == 0 || functionCount == 0)
			return SkillBuffStat2EvaluationReadinessStatus.NoFunctionPlans;
		if (functionPlans.Any(plan => plan.Status == SkillBuffStatFunctionRegistryPlanStatus.UnsupportedFunction))
			return SkillBuffStat2EvaluationReadinessStatus.UnsupportedFunctionPlan;
		if (!hasLiveStat2StateProvider)
			return SkillBuffStat2EvaluationReadinessStatus.BlockedMissingStat2StateProvider;
		if (!hasLiveCurrentValueFormulaProvider)
			return SkillBuffStat2EvaluationReadinessStatus.BlockedMissingCurrentValueFormulaProvider;
		if (!hasLiveAdditionStatProvider)
			return SkillBuffStat2EvaluationReadinessStatus.BlockedMissingAdditionStatProvider;
		if (!hasLiveReverseStatProvider)
			return SkillBuffStat2EvaluationReadinessStatus.BlockedMissingReverseStatProvider;
		if (!hasLiveStatFunctionApplyProvider)
			return SkillBuffStat2EvaluationReadinessStatus.BlockedMissingStatFunctionApplyProvider;
		if (!hasLiveStatCapProvider)
			return SkillBuffStat2EvaluationReadinessStatus.BlockedMissingStatCapProvider;
		return SkillBuffStat2EvaluationReadinessStatus.Ready;
	}
}

public enum SkillBuffStat2EvaluationReadinessStatus
{
	NoFunctionPlans,
	UnsupportedFunctionPlan,
	BlockedMissingStat2StateProvider,
	BlockedMissingCurrentValueFormulaProvider,
	BlockedMissingAdditionStatProvider,
	BlockedMissingReverseStatProvider,
	BlockedMissingStatFunctionApplyProvider,
	BlockedMissingStatCapProvider,
	Ready,
}

public sealed record SkillBuffStat2EvaluationReadinessReport(
	SkillBuffStat2EvaluationReadinessStatus Status,
	int FunctionPlanCount,
	int FunctionCount,
	IReadOnlyList<string> StatNames,
	int AddFunctionCount,
	int RateFunctionCount,
	int SetFunctionCount,
	int BonusFunctionCount,
	int BaseFunctionCount,
	int ConditionedFunctionCount,
	bool HasLiveStat2StateProvider,
	bool HasLiveCurrentValueFormulaProvider,
	bool HasLiveAdditionStatProvider,
	bool HasLiveReverseStatProvider,
	bool HasLiveStatFunctionApplyProvider,
	bool HasLiveStatCapProvider,
	string CurrentValueFormula,
	IReadOnlyList<string> MissingInputs,
	string JavaSource)
{
	public bool IsReadyForRuntimeEvaluation => Status == SkillBuffStat2EvaluationReadinessStatus.Ready;
}
