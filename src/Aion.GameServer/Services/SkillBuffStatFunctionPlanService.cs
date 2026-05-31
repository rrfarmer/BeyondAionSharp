using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public static class SkillBuffStatFunctionPlanService
{
	public static SkillBuffStatFunctionRegistryPlan CreateRegistryPlan(
		int skillId,
		string effectName,
		int skillLevel,
		IReadOnlyList<SkillStatChange> changes,
		bool hasLiveEffectStatOwnerProvider = false,
		bool hasLiveStatFunctionRegistryProvider = false,
		bool hasLiveConditionValidatorProvider = false)
	{
		var functions = changes
			.Select((change, index) => CreateFunctionPlan(index, skillLevel, change))
			.OrderBy(function => function.Priority)
			.ThenBy(function => function.SourceIndex)
			.ToArray();
		var missingInputs = new List<string>();

		if (!hasLiveEffectStatOwnerProvider)
			missingInputs.Add("live Effect StatOwner provider");
		if (!hasLiveStatFunctionRegistryProvider)
			missingInputs.Add("live CreatureGameStats stat-function registry");
		if (functions.Any(function => function.HasConditions) && !hasLiveConditionValidatorProvider)
			missingInputs.Add("live Conditions.validate provider");
		if (functions.Any(function => !function.IsSupported))
			missingInputs.Add("supported BufEffect stat function mapping");

		var status = DetermineStatus(
			functions,
			hasLiveEffectStatOwnerProvider,
			hasLiveStatFunctionRegistryProvider,
			hasLiveConditionValidatorProvider);
		return new SkillBuffStatFunctionRegistryPlan(
			status,
			skillId,
			effectName,
			skillLevel,
			functions,
			hasLiveEffectStatOwnerProvider,
			hasLiveStatFunctionRegistryProvider,
			hasLiveConditionValidatorProvider,
			missingInputs,
			"BufEffect.getModifiers creates StatAddFunction/StatRateFunction/StatSetFunction; CreatureGameStats.addEffect wraps generated functions with StatFunctionProxy(effect, function) before registry insertion when owner differs");
	}

	private static SkillBuffStatFunctionPlan CreateFunctionPlan(int sourceIndex, int skillLevel, SkillStatChange change)
	{
		var mapping = MapFunction(change.Func);
		return new SkillBuffStatFunctionPlan(
			sourceIndex,
			change.Stat,
			change.Func,
			change.Value,
			change.Delta,
			change.Value + change.Delta * skillLevel,
			mapping.JavaFunctionType,
			mapping.Priority,
			mapping.IsBonus,
			mapping.IsSupported,
			RequiresStatFunctionProxy: true,
			change.Conditions);
	}

	private static SkillBuffStatFunctionMapping MapFunction(string func)
	{
		return func switch
		{
			"ADD" => new SkillBuffStatFunctionMapping("StatAddFunction", 60, IsBonus: true, IsSupported: true),
			"PERCENT" => new SkillBuffStatFunctionMapping("StatRateFunction", 50, IsBonus: true, IsSupported: true),
			"REPLACE" => new SkillBuffStatFunctionMapping("StatSetFunction", 40, IsBonus: false, IsSupported: true),
			_ => new SkillBuffStatFunctionMapping("unsupported", int.MaxValue, IsBonus: false, IsSupported: false),
		};
	}

	private static SkillBuffStatFunctionRegistryPlanStatus DetermineStatus(
		IReadOnlyList<SkillBuffStatFunctionPlan> functions,
		bool hasLiveEffectStatOwnerProvider,
		bool hasLiveStatFunctionRegistryProvider,
		bool hasLiveConditionValidatorProvider)
	{
		if (functions.Count == 0)
			return SkillBuffStatFunctionRegistryPlanStatus.NoStatFunctions;
		if (functions.Any(function => !function.IsSupported))
			return SkillBuffStatFunctionRegistryPlanStatus.UnsupportedFunction;
		if (!hasLiveEffectStatOwnerProvider)
			return SkillBuffStatFunctionRegistryPlanStatus.BlockedMissingEffectStatOwnerProvider;
		if (!hasLiveStatFunctionRegistryProvider)
			return SkillBuffStatFunctionRegistryPlanStatus.BlockedMissingStatFunctionRegistryProvider;
		if (functions.Any(function => function.HasConditions) && !hasLiveConditionValidatorProvider)
			return SkillBuffStatFunctionRegistryPlanStatus.BlockedMissingConditionValidatorProvider;
		return SkillBuffStatFunctionRegistryPlanStatus.Ready;
	}

	private sealed record SkillBuffStatFunctionMapping(
		string JavaFunctionType,
		int Priority,
		bool IsBonus,
		bool IsSupported);
}

public enum SkillBuffStatFunctionRegistryPlanStatus
{
	NoStatFunctions,
	UnsupportedFunction,
	BlockedMissingEffectStatOwnerProvider,
	BlockedMissingStatFunctionRegistryProvider,
	BlockedMissingConditionValidatorProvider,
	Ready,
}

public sealed record SkillBuffStatFunctionRegistryPlan(
	SkillBuffStatFunctionRegistryPlanStatus Status,
	int SkillId,
	string EffectName,
	int SkillLevel,
	IReadOnlyList<SkillBuffStatFunctionPlan> Functions,
	bool HasLiveEffectStatOwnerProvider,
	bool HasLiveStatFunctionRegistryProvider,
	bool HasLiveConditionValidatorProvider,
	IReadOnlyList<string> MissingInputs,
	string JavaSource)
{
	public bool IsReadyForRegistry => Status == SkillBuffStatFunctionRegistryPlanStatus.Ready;
}

public sealed record SkillBuffStatFunctionPlan(
	int SourceIndex,
	string StatName,
	string Func,
	int Value,
	int Delta,
	int EffectiveValue,
	string JavaFunctionType,
	int Priority,
	bool IsBonus,
	bool IsSupported,
	bool RequiresStatFunctionProxy,
	IReadOnlyList<SkillStatChangeConditionSummary> Conditions)
{
	public bool HasConditions => Conditions.Count > 0;
}
