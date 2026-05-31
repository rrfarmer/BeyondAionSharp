namespace Aion.GameServer.Services;

public static class SkillBuffStatCapRecalculationReadinessReportService
{
	public static SkillBuffStatCapRecalculationReadinessReport CreateReport(
		IReadOnlyList<SkillBuffStatFunctionRegistryPlan> functionPlans,
		bool hasLiveCalculateBaseValueProvider = false,
		bool hasLiveCreatureAwareCapProvider = false,
		bool hasLiveAttackSpeedBonusClampProvider = false,
		bool hasLiveMaxHpMpRecalculationProvider = false)
	{
		var functions = functionPlans
			.SelectMany(plan => plan.Functions)
			.ToArray();
		var statNames = functions
			.Select(function => function.StatName)
			.Distinct(StringComparer.Ordinal)
			.Order(StringComparer.Ordinal)
			.ToArray();
		var requiresAttackSpeedBonusClamp = statNames.Contains("ATTACK_SPEED", StringComparer.Ordinal);
		var requiresElementalDefenseCaps = statNames.Any(IsElementalDefenseStat);
		var requiresSpeedUnrestrictedCap = statNames.Any(statName => statName is "SPEED" or "FLY_SPEED");
		var requiresMaxHpMpRecalculation = functions.Length > 0;
		var missingInputs = new List<string>();

		if (!hasLiveCalculateBaseValueProvider)
			missingInputs.Add("live StatCapUtil.calculateBaseValue provider");
		if (!hasLiveCreatureAwareCapProvider)
			missingInputs.Add("live StatCapUtil creature-aware lower/upper cap provider");
		if (requiresAttackSpeedBonusClamp && !hasLiveAttackSpeedBonusClampProvider)
			missingInputs.Add("live StatCapUtil ATTACK_SPEED bonus clamp provider");
		if (requiresMaxHpMpRecalculation && !hasLiveMaxHpMpRecalculationProvider)
			missingInputs.Add("live CreatureGameStats.onStatsChange max HP/MP recalculation provider");
		if (functionPlans.Any(plan => plan.Status == SkillBuffStatFunctionRegistryPlanStatus.UnsupportedFunction))
			missingInputs.Add("supported BufEffect stat function mapping");

		var status = DetermineStatus(
			functionPlans,
			functions.Length,
			requiresAttackSpeedBonusClamp,
			hasLiveCalculateBaseValueProvider,
			hasLiveCreatureAwareCapProvider,
			hasLiveAttackSpeedBonusClampProvider,
			hasLiveMaxHpMpRecalculationProvider);
		return new SkillBuffStatCapRecalculationReadinessReport(
			status,
			functionPlans.Count,
			functions.Length,
			statNames,
			requiresAttackSpeedBonusClamp,
			requiresElementalDefenseCaps,
			requiresSpeedUnrestrictedCap,
			requiresMaxHpMpRecalculation,
			hasLiveCalculateBaseValueProvider,
			hasLiveCreatureAwareCapProvider,
			hasLiveAttackSpeedBonusClampProvider,
			hasLiveMaxHpMpRecalculationProvider,
			missingInputs,
			"CreatureGameStats.getStat applies StatCapUtil.calculateBaseValue after stat functions; StatCapUtil clamps current value to creature-aware lower/upper caps and special-cases ATTACK_SPEED bonus; CreatureGameStats.onStatsChange rescales current HP/MP when MAXHP/MAXMP change");
	}

	private static SkillBuffStatCapRecalculationReadinessStatus DetermineStatus(
		IReadOnlyList<SkillBuffStatFunctionRegistryPlan> functionPlans,
		int functionCount,
		bool requiresAttackSpeedBonusClamp,
		bool hasLiveCalculateBaseValueProvider,
		bool hasLiveCreatureAwareCapProvider,
		bool hasLiveAttackSpeedBonusClampProvider,
		bool hasLiveMaxHpMpRecalculationProvider)
	{
		if (functionPlans.Count == 0 || functionCount == 0)
			return SkillBuffStatCapRecalculationReadinessStatus.NoFunctionPlans;
		if (functionPlans.Any(plan => plan.Status == SkillBuffStatFunctionRegistryPlanStatus.UnsupportedFunction))
			return SkillBuffStatCapRecalculationReadinessStatus.UnsupportedFunctionPlan;
		if (!hasLiveCalculateBaseValueProvider)
			return SkillBuffStatCapRecalculationReadinessStatus.BlockedMissingCalculateBaseValueProvider;
		if (!hasLiveCreatureAwareCapProvider)
			return SkillBuffStatCapRecalculationReadinessStatus.BlockedMissingCreatureAwareCapProvider;
		if (requiresAttackSpeedBonusClamp && !hasLiveAttackSpeedBonusClampProvider)
			return SkillBuffStatCapRecalculationReadinessStatus.BlockedMissingAttackSpeedBonusClampProvider;
		if (!hasLiveMaxHpMpRecalculationProvider)
			return SkillBuffStatCapRecalculationReadinessStatus.BlockedMissingMaxHpMpRecalculationProvider;
		return SkillBuffStatCapRecalculationReadinessStatus.Ready;
	}

	private static bool IsElementalDefenseStat(string statName)
	{
		return statName is "WATER_RESISTANCE"
			or "FIRE_RESISTANCE"
			or "EARTH_RESISTANCE"
			or "WIND_RESISTANCE"
			or "DARK_RESISTANCE"
			or "LIGHT_RESISTANCE";
	}
}

public enum SkillBuffStatCapRecalculationReadinessStatus
{
	NoFunctionPlans,
	UnsupportedFunctionPlan,
	BlockedMissingCalculateBaseValueProvider,
	BlockedMissingCreatureAwareCapProvider,
	BlockedMissingAttackSpeedBonusClampProvider,
	BlockedMissingMaxHpMpRecalculationProvider,
	Ready,
}

public sealed record SkillBuffStatCapRecalculationReadinessReport(
	SkillBuffStatCapRecalculationReadinessStatus Status,
	int FunctionPlanCount,
	int FunctionCount,
	IReadOnlyList<string> StatNames,
	bool RequiresAttackSpeedBonusClamp,
	bool RequiresElementalDefenseCaps,
	bool RequiresSpeedUnrestrictedCap,
	bool RequiresMaxHpMpRecalculation,
	bool HasLiveCalculateBaseValueProvider,
	bool HasLiveCreatureAwareCapProvider,
	bool HasLiveAttackSpeedBonusClampProvider,
	bool HasLiveMaxHpMpRecalculationProvider,
	IReadOnlyList<string> MissingInputs,
	string JavaSource)
{
	public bool IsReadyForStatCapRecalculation => Status == SkillBuffStatCapRecalculationReadinessStatus.Ready;
}
