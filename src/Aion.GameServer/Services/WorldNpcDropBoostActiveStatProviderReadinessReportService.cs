using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public static class WorldNpcDropBoostActiveStatProviderReadinessReportService
{
	public static WorldNpcDropBoostActiveStatProviderReadinessReport CreateReport(
		SkillTemplateTable? skillTemplates,
		bool hasLiveActiveEffectControllerProvider = false,
		bool hasLiveEffectStatOwnerProvider = false,
		bool hasLiveStatFunctionRegistryProvider = false,
		bool hasLiveCreatureGameStatsStatQueryProvider = false,
		bool hasLiveConditionValidatorProvider = false,
		int statFunctionPlanSkillLevel = 1)
	{
		var staticMetadataReport = WorldNpcDropBoostStatProviderReadinessReportService.CreateReport(
			skillTemplates,
			hasLiveEffectStateProvider: hasLiveActiveEffectControllerProvider,
			hasLiveCreatureGameStatsProvider: hasLiveCreatureGameStatsStatQueryProvider);
		var conditionReadinessReport = SkillStatChangeConditionReadinessReportService.CreateReport(
			skillTemplates,
			hasLiveConditionValidatorProvider);
		var statFunctionPlans = CreateStatFunctionPlans(
			skillTemplates,
			statFunctionPlanSkillLevel,
			hasLiveEffectStatOwnerProvider,
			hasLiveStatFunctionRegistryProvider,
			hasLiveConditionValidatorProvider);
		var missingInputs = new List<string>();

		if (skillTemplates == null)
		{
			missingInputs.Add("skill_templates");
		}
		else
		{
			if (staticMetadataReport.DropBoostEffectCount == 0 || staticMetadataReport.BoostDropRateChangeCount == 0)
				missingInputs.Add("static boostdroprate BOOST_DROP_RATE metadata");
			if (staticMetadataReport.DrBoostEffectCount == 0 || staticMetadataReport.DrBoostChangeCount == 0)
				missingInputs.Add("static drboost DR_BOOST metadata");
		}

		if (!hasLiveActiveEffectControllerProvider)
			missingInputs.Add("live EffectController active-effect provider");
		if (!hasLiveEffectStatOwnerProvider)
			missingInputs.Add("live Effect StatOwner provider");
		if (!hasLiveStatFunctionRegistryProvider)
			missingInputs.Add("live CreatureGameStats stat-function registry");
		if (!hasLiveCreatureGameStatsStatQueryProvider)
			missingInputs.Add("live CreatureGameStats.getStat provider");
		if (conditionReadinessReport.ConditionEntryCount > 0 && !hasLiveConditionValidatorProvider)
			missingInputs.Add("live Conditions.validate provider");
		if (statFunctionPlans.Any(plan => plan.Status == SkillBuffStatFunctionRegistryPlanStatus.UnsupportedFunction))
			missingInputs.Add("supported BufEffect stat function mapping");

		var status = DetermineStatus(
			skillTemplates,
			staticMetadataReport,
			conditionReadinessReport,
			statFunctionPlans,
			hasLiveActiveEffectControllerProvider,
			hasLiveEffectStatOwnerProvider,
			hasLiveStatFunctionRegistryProvider,
			hasLiveCreatureGameStatsStatQueryProvider,
			hasLiveConditionValidatorProvider);
		return new WorldNpcDropBoostActiveStatProviderReadinessReport(
			status,
			staticMetadataReport,
			conditionReadinessReport,
			statFunctionPlans,
			hasLiveActiveEffectControllerProvider,
			hasLiveEffectStatOwnerProvider,
			hasLiveStatFunctionRegistryProvider,
			hasLiveCreatureGameStatsStatQueryProvider,
			hasLiveConditionValidatorProvider,
			missingInputs,
			"DropRegistrationService.calculateBoostDropRate -> CreatureGameStats.getStat; EffectController.addEffect -> Effect.startEffect -> BufEffect.startEffect -> CreatureGameStats.addEffect(Effect, modifiers); CreatureGameStats.endEffect removes functions by StatOwner");
	}

	private static WorldNpcDropBoostActiveStatProviderReadinessStatus DetermineStatus(
		SkillTemplateTable? skillTemplates,
		WorldNpcDropBoostStatProviderReadinessReport staticMetadataReport,
		SkillStatChangeConditionReadinessReport conditionReadinessReport,
		IReadOnlyList<SkillBuffStatFunctionRegistryPlan> statFunctionPlans,
		bool hasLiveActiveEffectControllerProvider,
		bool hasLiveEffectStatOwnerProvider,
		bool hasLiveStatFunctionRegistryProvider,
		bool hasLiveCreatureGameStatsStatQueryProvider,
		bool hasLiveConditionValidatorProvider)
	{
		if (skillTemplates == null)
			return WorldNpcDropBoostActiveStatProviderReadinessStatus.MissingSkillTemplates;
		if (staticMetadataReport.DropBoostEffectCount == 0
			|| staticMetadataReport.BoostDropRateChangeCount == 0
			|| staticMetadataReport.DrBoostEffectCount == 0
			|| staticMetadataReport.DrBoostChangeCount == 0)
			return WorldNpcDropBoostActiveStatProviderReadinessStatus.MissingStaticMetadata;
		if (statFunctionPlans.Any(plan => plan.Status == SkillBuffStatFunctionRegistryPlanStatus.UnsupportedFunction))
			return WorldNpcDropBoostActiveStatProviderReadinessStatus.UnsupportedStatFunctionPlan;
		if (!hasLiveActiveEffectControllerProvider)
			return WorldNpcDropBoostActiveStatProviderReadinessStatus.BlockedMissingActiveEffectControllerProvider;
		if (!hasLiveEffectStatOwnerProvider)
			return WorldNpcDropBoostActiveStatProviderReadinessStatus.BlockedMissingEffectStatOwnerProvider;
		if (!hasLiveStatFunctionRegistryProvider)
			return WorldNpcDropBoostActiveStatProviderReadinessStatus.BlockedMissingStatFunctionRegistryProvider;
		if (!hasLiveCreatureGameStatsStatQueryProvider)
			return WorldNpcDropBoostActiveStatProviderReadinessStatus.BlockedMissingCreatureGameStatsStatQueryProvider;
		if (conditionReadinessReport.ConditionEntryCount > 0 && !hasLiveConditionValidatorProvider)
			return WorldNpcDropBoostActiveStatProviderReadinessStatus.BlockedMissingConditionValidatorProvider;
		return WorldNpcDropBoostActiveStatProviderReadinessStatus.Ready;
	}

	private static IReadOnlyList<SkillBuffStatFunctionRegistryPlan> CreateStatFunctionPlans(
		SkillTemplateTable? skillTemplates,
		int skillLevel,
		bool hasLiveEffectStatOwnerProvider,
		bool hasLiveStatFunctionRegistryProvider,
		bool hasLiveConditionValidatorProvider)
	{
		if (skillTemplates == null)
			return Array.Empty<SkillBuffStatFunctionRegistryPlan>();

		return skillTemplates.Templates
			.SelectMany(template => template.BuffStatEffects
				.Where(effect => string.Equals(effect.EffectName, "boostdroprate", StringComparison.Ordinal)
					|| string.Equals(effect.EffectName, "drboost", StringComparison.Ordinal))
				.Select(effect => SkillBuffStatFunctionPlanService.CreateRegistryPlan(
					template.SkillId,
					effect.EffectName,
					skillLevel,
					effect.Changes,
					hasLiveEffectStatOwnerProvider,
					hasLiveStatFunctionRegistryProvider,
					hasLiveConditionValidatorProvider)))
			.ToArray();
	}
}

public enum WorldNpcDropBoostActiveStatProviderReadinessStatus
{
	MissingSkillTemplates,
	MissingStaticMetadata,
	UnsupportedStatFunctionPlan,
	BlockedMissingActiveEffectControllerProvider,
	BlockedMissingEffectStatOwnerProvider,
	BlockedMissingStatFunctionRegistryProvider,
	BlockedMissingCreatureGameStatsStatQueryProvider,
	BlockedMissingConditionValidatorProvider,
	Ready,
}

public sealed record WorldNpcDropBoostActiveStatProviderReadinessReport(
	WorldNpcDropBoostActiveStatProviderReadinessStatus Status,
	WorldNpcDropBoostStatProviderReadinessReport StaticMetadataReport,
	SkillStatChangeConditionReadinessReport ConditionReadinessReport,
	IReadOnlyList<SkillBuffStatFunctionRegistryPlan> StatFunctionPlans,
	bool HasLiveActiveEffectControllerProvider,
	bool HasLiveEffectStatOwnerProvider,
	bool HasLiveStatFunctionRegistryProvider,
	bool HasLiveCreatureGameStatsStatQueryProvider,
	bool HasLiveConditionValidatorProvider,
	IReadOnlyList<string> MissingInputs,
	string JavaSource)
{
	public bool IsReadyForDropWorkflow => Status == WorldNpcDropBoostActiveStatProviderReadinessStatus.Ready;

	public int StatFunctionPlanCount => StatFunctionPlans.Count;
}
