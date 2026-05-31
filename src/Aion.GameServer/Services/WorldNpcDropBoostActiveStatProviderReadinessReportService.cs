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
		int statFunctionPlanSkillLevel = 1,
		bool hasLiveConcurrentStatFunctionStorage = false,
		bool hasLiveStatFunctionInsertionProvider = false,
		bool hasLiveStatFunctionRemovalProvider = false,
		bool hasLiveSortedStatFunctionSnapshotProvider = false,
		bool hasLiveStatsChangeRecalculationProvider = false,
		bool hasLiveStat2StateProvider = false,
		bool hasLiveCurrentValueFormulaProvider = false,
		bool hasLiveAdditionStatProvider = false,
		bool hasLiveReverseStatProvider = false,
		bool hasLiveStatFunctionApplyProvider = false,
		bool hasLiveStatCapProvider = false,
		bool hasLiveCalculateBaseValueProvider = false,
		bool hasLiveCreatureAwareCapProvider = false,
		bool hasLiveAttackSpeedBonusClampProvider = false,
		bool hasLiveMaxHpMpRecalculationProvider = false)
	{
		var staticMetadataReport = WorldNpcDropBoostStatProviderReadinessReportService.CreateReport(
			skillTemplates,
			hasLiveEffectStateProvider: hasLiveActiveEffectControllerProvider,
			hasLiveCreatureGameStatsProvider: hasLiveCreatureGameStatsStatQueryProvider);
		var conditionReadinessReport = SkillStatChangeConditionReadinessReportService.CreateReport(
			skillTemplates,
			hasLiveConditionValidatorProvider);
		var conditionPreviewCoverageReport = SkillStatConditionPreviewCoverageReportService.CreateReport(skillTemplates);
		var statFunctionPlans = CreateStatFunctionPlans(
			skillTemplates,
			statFunctionPlanSkillLevel,
			hasLiveEffectStatOwnerProvider,
			hasLiveStatFunctionRegistryProvider,
			hasLiveConditionValidatorProvider);
		var statFunctionRegistryReadinessReport = SkillBuffStatFunctionRegistryReadinessReportService.CreateReport(
			statFunctionPlans,
			hasLiveConcurrentStatFunctionStorage,
			hasLiveStatFunctionInsertionProvider,
			hasLiveStatFunctionRemovalProvider,
			hasLiveSortedStatFunctionSnapshotProvider,
			hasLiveStatsChangeRecalculationProvider);
		var stat2EvaluationReadinessReport = SkillBuffStat2EvaluationReadinessReportService.CreateReport(
			statFunctionPlans,
			hasLiveStat2StateProvider,
			hasLiveCurrentValueFormulaProvider,
			hasLiveAdditionStatProvider,
			hasLiveReverseStatProvider,
			hasLiveStatFunctionApplyProvider,
			hasLiveStatCapProvider);
		var statCapRecalculationReadinessReport = SkillBuffStatCapRecalculationReadinessReportService.CreateReport(
			statFunctionPlans,
			hasLiveCalculateBaseValueProvider,
			hasLiveCreatureAwareCapProvider,
			hasLiveAttackSpeedBonusClampProvider,
			hasLiveMaxHpMpRecalculationProvider);
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
		if (conditionPreviewCoverageReport.Status == SkillStatConditionPreviewCoverageStatus.BlockedUnsupportedConditions)
			missingInputs.Add("supported isolated stat-condition preview coverage");
		foreach (var missingInput in conditionPreviewCoverageReport.MissingInputs)
		{
			if (!missingInputs.Contains(missingInput, StringComparer.Ordinal))
				missingInputs.Add(missingInput);
		}
		if (statFunctionPlans.Any(plan => plan.Status == SkillBuffStatFunctionRegistryPlanStatus.UnsupportedFunction))
			missingInputs.Add("supported BufEffect stat function mapping");
		foreach (var missingInput in statFunctionRegistryReadinessReport.MissingInputs)
		{
			if (!missingInputs.Contains(missingInput, StringComparer.Ordinal))
				missingInputs.Add(missingInput);
		}
		foreach (var missingInput in stat2EvaluationReadinessReport.MissingInputs)
		{
			if (!missingInputs.Contains(missingInput, StringComparer.Ordinal))
				missingInputs.Add(missingInput);
		}
		foreach (var missingInput in statCapRecalculationReadinessReport.MissingInputs)
		{
			if (!missingInputs.Contains(missingInput, StringComparer.Ordinal))
				missingInputs.Add(missingInput);
		}

		var status = DetermineStatus(
			skillTemplates,
			staticMetadataReport,
			conditionReadinessReport,
			conditionPreviewCoverageReport,
			statFunctionPlans,
			statFunctionRegistryReadinessReport,
			stat2EvaluationReadinessReport,
			statCapRecalculationReadinessReport,
			hasLiveActiveEffectControllerProvider,
			hasLiveEffectStatOwnerProvider,
			hasLiveStatFunctionRegistryProvider,
			hasLiveCreatureGameStatsStatQueryProvider,
			hasLiveConditionValidatorProvider);
		return new WorldNpcDropBoostActiveStatProviderReadinessReport(
			status,
			staticMetadataReport,
			conditionReadinessReport,
			conditionPreviewCoverageReport,
			statFunctionPlans,
			statFunctionRegistryReadinessReport,
			stat2EvaluationReadinessReport,
			statCapRecalculationReadinessReport,
			hasLiveActiveEffectControllerProvider,
			hasLiveEffectStatOwnerProvider,
			hasLiveStatFunctionRegistryProvider,
			hasLiveCreatureGameStatsStatQueryProvider,
			hasLiveConditionValidatorProvider,
			missingInputs,
			"DropRegistrationService.calculateBoostDropRate -> CreatureGameStats.getStat -> Stat2.getCurrent -> StatCapUtil.calculateBaseValue; EffectController.addEffect -> Effect.startEffect -> BufEffect.startEffect -> CreatureGameStats.addEffect(Effect, modifiers); CreatureGameStats.onStatsChange rescales max HP/MP; CreatureGameStats.endEffect removes functions by StatOwner");
	}

	private static WorldNpcDropBoostActiveStatProviderReadinessStatus DetermineStatus(
		SkillTemplateTable? skillTemplates,
		WorldNpcDropBoostStatProviderReadinessReport staticMetadataReport,
		SkillStatChangeConditionReadinessReport conditionReadinessReport,
		SkillStatConditionPreviewCoverageReport conditionPreviewCoverageReport,
		IReadOnlyList<SkillBuffStatFunctionRegistryPlan> statFunctionPlans,
		SkillBuffStatFunctionRegistryReadinessReport statFunctionRegistryReadinessReport,
		SkillBuffStat2EvaluationReadinessReport stat2EvaluationReadinessReport,
		SkillBuffStatCapRecalculationReadinessReport statCapRecalculationReadinessReport,
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
		if (conditionPreviewCoverageReport.Status == SkillStatConditionPreviewCoverageStatus.BlockedUnsupportedConditions)
			return WorldNpcDropBoostActiveStatProviderReadinessStatus.BlockedUnsupportedConditionPreviewCoverage;
		if (conditionPreviewCoverageReport.Status == SkillStatConditionPreviewCoverageStatus.BlockedStaticMetadata)
			return WorldNpcDropBoostActiveStatProviderReadinessStatus.BlockedStaticConditionPreviewMetadata;
		if (!hasLiveActiveEffectControllerProvider)
			return WorldNpcDropBoostActiveStatProviderReadinessStatus.BlockedMissingActiveEffectControllerProvider;
		if (!hasLiveEffectStatOwnerProvider)
			return WorldNpcDropBoostActiveStatProviderReadinessStatus.BlockedMissingEffectStatOwnerProvider;
		if (!hasLiveStatFunctionRegistryProvider)
			return WorldNpcDropBoostActiveStatProviderReadinessStatus.BlockedMissingStatFunctionRegistryProvider;
		if (!statFunctionRegistryReadinessReport.IsReadyForLiveRegistry)
			return WorldNpcDropBoostActiveStatProviderReadinessStatus.BlockedMissingStatFunctionRegistryReadiness;
		if (!stat2EvaluationReadinessReport.IsReadyForRuntimeEvaluation)
			return WorldNpcDropBoostActiveStatProviderReadinessStatus.BlockedMissingStat2EvaluationReadiness;
		if (!statCapRecalculationReadinessReport.IsReadyForStatCapRecalculation)
			return WorldNpcDropBoostActiveStatProviderReadinessStatus.BlockedMissingStatCapRecalculationReadiness;
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
	BlockedUnsupportedConditionPreviewCoverage,
	BlockedStaticConditionPreviewMetadata,
	BlockedMissingActiveEffectControllerProvider,
	BlockedMissingEffectStatOwnerProvider,
	BlockedMissingStatFunctionRegistryProvider,
	BlockedMissingStatFunctionRegistryReadiness,
	BlockedMissingStat2EvaluationReadiness,
	BlockedMissingStatCapRecalculationReadiness,
	BlockedMissingCreatureGameStatsStatQueryProvider,
	BlockedMissingConditionValidatorProvider,
	Ready,
}

public sealed record WorldNpcDropBoostActiveStatProviderReadinessReport(
	WorldNpcDropBoostActiveStatProviderReadinessStatus Status,
	WorldNpcDropBoostStatProviderReadinessReport StaticMetadataReport,
	SkillStatChangeConditionReadinessReport ConditionReadinessReport,
	SkillStatConditionPreviewCoverageReport ConditionPreviewCoverageReport,
	IReadOnlyList<SkillBuffStatFunctionRegistryPlan> StatFunctionPlans,
	SkillBuffStatFunctionRegistryReadinessReport StatFunctionRegistryReadinessReport,
	SkillBuffStat2EvaluationReadinessReport Stat2EvaluationReadinessReport,
	SkillBuffStatCapRecalculationReadinessReport StatCapRecalculationReadinessReport,
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
