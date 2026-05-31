namespace Aion.GameServer.Services;

public static class SkillBuffStatFunctionRegistryReadinessReportService
{
	public static SkillBuffStatFunctionRegistryReadinessReport CreateReport(
		IReadOnlyList<SkillBuffStatFunctionRegistryPlan> functionPlans,
		bool hasLiveConcurrentStatFunctionStorage = false,
		bool hasLiveStatFunctionInsertionProvider = false,
		bool hasLiveStatFunctionRemovalProvider = false,
		bool hasLiveSortedSnapshotProvider = false,
		bool hasLiveStatsChangeRecalculationProvider = false)
	{
		var functions = functionPlans
			.SelectMany(plan => plan.Functions)
			.ToArray();
		var statBuckets = functions
			.GroupBy(function => function.StatName, StringComparer.Ordinal)
			.OrderBy(group => group.Key, StringComparer.Ordinal)
			.Select(group => new SkillBuffStatFunctionRegistryStatBucket(
				group.Key,
				group.OrderBy(function => function.Priority)
					.ThenBy(function => function.SourceIndex)
					.ToArray()))
			.ToArray();
		var missingInputs = new List<string>();

		if (!hasLiveConcurrentStatFunctionStorage)
			missingInputs.Add("live ConcurrentHashMap<StatEnum, List<IStatFunction>> equivalent");
		if (!hasLiveStatFunctionInsertionProvider)
			missingInputs.Add("live CreatureGameStats.addEffectOnly insertion provider");
		if (!hasLiveStatFunctionRemovalProvider)
			missingInputs.Add("live CreatureGameStats.endEffect removal provider");
		if (!hasLiveSortedSnapshotProvider)
			missingInputs.Add("live CreatureGameStats.getStatsSorted snapshot provider");
		if (!hasLiveStatsChangeRecalculationProvider)
			missingInputs.Add("live CreatureGameStats.onStatsChange recalculation provider");
		if (functionPlans.Any(plan => plan.Status == SkillBuffStatFunctionRegistryPlanStatus.UnsupportedFunction))
			missingInputs.Add("supported BufEffect stat function mapping");

		var status = DetermineStatus(
			functionPlans,
			functions.Length,
			hasLiveConcurrentStatFunctionStorage,
			hasLiveStatFunctionInsertionProvider,
			hasLiveStatFunctionRemovalProvider,
			hasLiveSortedSnapshotProvider,
			hasLiveStatsChangeRecalculationProvider);
		return new SkillBuffStatFunctionRegistryReadinessReport(
			status,
			functionPlans.Count,
			functions.Length,
			statBuckets,
			functions.Count(function => function.RequiresStatFunctionProxy),
			functions.Count(function => function.HasConditions),
			hasLiveConcurrentStatFunctionStorage,
			hasLiveStatFunctionInsertionProvider,
			hasLiveStatFunctionRemovalProvider,
			hasLiveSortedSnapshotProvider,
			hasLiveStatsChangeRecalculationProvider,
			missingInputs,
			"CreatureGameStats.addEffectOnly -> ConcurrentHashMap.compute per StatEnum, StatFunctionProxy(effect, function), List.sort(IStatFunction.compareTo); getStatsSorted returns a locked copy; endEffect removes functions whose owner equals the StatOwner; addEffect/endEffect call onStatsChange");
	}

	private static SkillBuffStatFunctionRegistryReadinessStatus DetermineStatus(
		IReadOnlyList<SkillBuffStatFunctionRegistryPlan> functionPlans,
		int functionCount,
		bool hasLiveConcurrentStatFunctionStorage,
		bool hasLiveStatFunctionInsertionProvider,
		bool hasLiveStatFunctionRemovalProvider,
		bool hasLiveSortedSnapshotProvider,
		bool hasLiveStatsChangeRecalculationProvider)
	{
		if (functionPlans.Count == 0 || functionCount == 0)
			return SkillBuffStatFunctionRegistryReadinessStatus.NoFunctionPlans;
		if (functionPlans.Any(plan => plan.Status == SkillBuffStatFunctionRegistryPlanStatus.UnsupportedFunction))
			return SkillBuffStatFunctionRegistryReadinessStatus.UnsupportedFunctionPlan;
		if (!hasLiveConcurrentStatFunctionStorage)
			return SkillBuffStatFunctionRegistryReadinessStatus.BlockedMissingConcurrentStatFunctionStorage;
		if (!hasLiveStatFunctionInsertionProvider)
			return SkillBuffStatFunctionRegistryReadinessStatus.BlockedMissingInsertionProvider;
		if (!hasLiveStatFunctionRemovalProvider)
			return SkillBuffStatFunctionRegistryReadinessStatus.BlockedMissingRemovalProvider;
		if (!hasLiveSortedSnapshotProvider)
			return SkillBuffStatFunctionRegistryReadinessStatus.BlockedMissingSortedSnapshotProvider;
		if (!hasLiveStatsChangeRecalculationProvider)
			return SkillBuffStatFunctionRegistryReadinessStatus.BlockedMissingStatsChangeRecalculationProvider;
		return SkillBuffStatFunctionRegistryReadinessStatus.Ready;
	}
}

public enum SkillBuffStatFunctionRegistryReadinessStatus
{
	NoFunctionPlans,
	UnsupportedFunctionPlan,
	BlockedMissingConcurrentStatFunctionStorage,
	BlockedMissingInsertionProvider,
	BlockedMissingRemovalProvider,
	BlockedMissingSortedSnapshotProvider,
	BlockedMissingStatsChangeRecalculationProvider,
	Ready,
}

public sealed record SkillBuffStatFunctionRegistryReadinessReport(
	SkillBuffStatFunctionRegistryReadinessStatus Status,
	int FunctionPlanCount,
	int FunctionCount,
	IReadOnlyList<SkillBuffStatFunctionRegistryStatBucket> StatBuckets,
	int RequiresProxyCount,
	int ConditionedFunctionCount,
	bool HasLiveConcurrentStatFunctionStorage,
	bool HasLiveStatFunctionInsertionProvider,
	bool HasLiveStatFunctionRemovalProvider,
	bool HasLiveSortedSnapshotProvider,
	bool HasLiveStatsChangeRecalculationProvider,
	IReadOnlyList<string> MissingInputs,
	string JavaSource)
{
	public bool IsReadyForLiveRegistry => Status == SkillBuffStatFunctionRegistryReadinessStatus.Ready;
}

public sealed record SkillBuffStatFunctionRegistryStatBucket(
	string StatName,
	IReadOnlyList<SkillBuffStatFunctionPlan> Functions)
{
	public int FunctionCount => Functions.Count;
}
