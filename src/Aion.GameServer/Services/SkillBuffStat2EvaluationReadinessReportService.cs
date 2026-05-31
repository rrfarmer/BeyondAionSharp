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
		bool hasLiveStatCapProvider = false,
		bool hasLiveNegativeSpeedRateFunctionProvider = false)
	{
		var functions = functionPlans
			.SelectMany(plan => plan.Functions)
			.ToArray();
		var negativeSpeedRateFunctions = functions
			.Where(IsNegativeSpeedRateFunction)
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
		if (negativeSpeedRateFunctions.Length > 0 && !hasLiveNegativeSpeedRateFunctionProvider)
			missingInputs.Add("live StatRateFunction negative SPEED current-value base provider");
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
			hasLiveStatCapProvider,
			negativeSpeedRateFunctions.Length,
			hasLiveNegativeSpeedRateFunctionProvider);
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
			negativeSpeedRateFunctions.Length,
			negativeSpeedRateFunctions.Length > 0,
			hasLiveStat2StateProvider,
			hasLiveCurrentValueFormulaProvider,
			hasLiveAdditionStatProvider,
			hasLiveReverseStatProvider,
			hasLiveStatFunctionApplyProvider,
			hasLiveStatCapProvider,
			hasLiveNegativeSpeedRateFunctionProvider,
			JavaCurrentValueFormula,
			missingInputs,
			"CreatureGameStats.getStat creates AdditionStat or ReverseStat, applies sorted IStatFunction instances that validate(stat), then calls StatCapUtil.calculateBaseValue; Stat2.getCurrent truncates base * baseRate + bonus * bonusRate + base * fixedBonusRate; StatRateFunction uses current value instead of baseWithoutBaseRate for negative bonus SPEED when bonus is already negative");
	}

	private static SkillBuffStat2EvaluationReadinessStatus DetermineStatus(
		IReadOnlyList<SkillBuffStatFunctionRegistryPlan> functionPlans,
		int functionCount,
		bool hasLiveStat2StateProvider,
		bool hasLiveCurrentValueFormulaProvider,
		bool hasLiveAdditionStatProvider,
		bool hasLiveReverseStatProvider,
		bool hasLiveStatFunctionApplyProvider,
		bool hasLiveStatCapProvider,
		int negativeSpeedRateFunctionCount,
		bool hasLiveNegativeSpeedRateFunctionProvider)
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
		if (negativeSpeedRateFunctionCount > 0 && !hasLiveNegativeSpeedRateFunctionProvider)
			return SkillBuffStat2EvaluationReadinessStatus.BlockedMissingNegativeSpeedRateFunctionProvider;
		return SkillBuffStat2EvaluationReadinessStatus.Ready;
	}

	private static bool IsNegativeSpeedRateFunction(SkillBuffStatFunctionPlan function)
	{
		return string.Equals(function.StatName, "SPEED", StringComparison.Ordinal)
			&& string.Equals(function.JavaFunctionType, "StatRateFunction", StringComparison.Ordinal)
			&& function.IsBonus
			&& function.EffectiveValue < 0;
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
	BlockedMissingNegativeSpeedRateFunctionProvider,
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
	int NegativeSpeedRateFunctionCount,
	bool RequiresNegativeSpeedRateFunctionHandling,
	bool HasLiveStat2StateProvider,
	bool HasLiveCurrentValueFormulaProvider,
	bool HasLiveAdditionStatProvider,
	bool HasLiveReverseStatProvider,
	bool HasLiveStatFunctionApplyProvider,
	bool HasLiveStatCapProvider,
	bool HasLiveNegativeSpeedRateFunctionProvider,
	string CurrentValueFormula,
	IReadOnlyList<string> MissingInputs,
	string JavaSource)
{
	public bool IsReadyForRuntimeEvaluation => Status == SkillBuffStat2EvaluationReadinessStatus.Ready;
}
