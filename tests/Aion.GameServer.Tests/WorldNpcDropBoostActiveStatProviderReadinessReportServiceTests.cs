using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcDropBoostActiveStatProviderReadinessReportServiceTests
{
	[Fact]
	public void CreateReport_RecordsMissingSkillTemplatesAndLiveProviderChain()
	{
		var report = WorldNpcDropBoostActiveStatProviderReadinessReportService.CreateReport(null);

		Assert.Equal(WorldNpcDropBoostActiveStatProviderReadinessStatus.MissingSkillTemplates, report.Status);
		Assert.False(report.IsReadyForDropWorkflow);
		Assert.Contains("skill_templates", report.MissingInputs);
		Assert.Contains("live EffectController active-effect provider", report.MissingInputs);
		Assert.Contains("live Effect StatOwner provider", report.MissingInputs);
		Assert.Contains("live CreatureGameStats stat-function registry", report.MissingInputs);
		Assert.Contains("live CreatureGameStats.getStat provider", report.MissingInputs);
		Assert.Contains("EffectController.addEffect", report.JavaSource, StringComparison.Ordinal);
		Assert.Equal(WorldNpcDropBoostStatProviderReadinessStatus.MissingSkillTemplates, report.StaticMetadataReport.Status);
		Assert.Equal(SkillStatChangeConditionReadinessStatus.MissingSkillTemplates, report.ConditionReadinessReport.Status);
		Assert.Equal(SkillStatConditionPreviewCoverageStatus.MissingSkillTemplates, report.ConditionPreviewCoverageReport.Status);
		Assert.Empty(report.StatFunctionPlans);
		Assert.Equal(SkillBuffStatFunctionRegistryReadinessStatus.NoFunctionPlans, report.StatFunctionRegistryReadinessReport.Status);
		Assert.Equal(SkillBuffStat2EvaluationReadinessStatus.NoFunctionPlans, report.Stat2EvaluationReadinessReport.Status);
		Assert.Equal(SkillBuffStatCapRecalculationReadinessStatus.NoFunctionPlans, report.StatCapRecalculationReadinessReport.Status);
	}

	[Fact]
	public void CreateReport_BlocksAfterStaticMetadataWhenActiveEffectProviderIsMissing()
	{
		var report = WorldNpcDropBoostActiveStatProviderReadinessReportService.CreateReport(CreateDropBoostSkillTemplates());

		Assert.Equal(WorldNpcDropBoostActiveStatProviderReadinessStatus.BlockedMissingActiveEffectControllerProvider, report.Status);
		Assert.False(report.IsReadyForDropWorkflow);
		Assert.Equal(WorldNpcDropBoostStatProviderReadinessStatus.BlockedMissingLiveEffectStateProvider, report.StaticMetadataReport.Status);
		Assert.Equal(SkillStatChangeConditionReadinessStatus.NoConditionMetadata, report.ConditionReadinessReport.Status);
		Assert.Equal(SkillStatConditionPreviewCoverageStatus.NoConditionedChanges, report.ConditionPreviewCoverageReport.Status);
		Assert.DoesNotContain("static boostdroprate BOOST_DROP_RATE metadata", report.MissingInputs);
		Assert.DoesNotContain("static drboost DR_BOOST metadata", report.MissingInputs);
		Assert.Contains("live EffectController active-effect provider", report.MissingInputs);
		Assert.Contains("live Effect StatOwner provider", report.MissingInputs);
		Assert.Contains("live CreatureGameStats stat-function registry", report.MissingInputs);
		Assert.Contains("live CreatureGameStats.getStat provider", report.MissingInputs);
		Assert.DoesNotContain("live Conditions.validate provider", report.MissingInputs);
		Assert.Equal(2, report.StatFunctionPlanCount);
		Assert.All(report.StatFunctionPlans, plan => Assert.Equal(SkillBuffStatFunctionRegistryPlanStatus.BlockedMissingEffectStatOwnerProvider, plan.Status));
		Assert.Equal(SkillBuffStatFunctionRegistryReadinessStatus.BlockedMissingConcurrentStatFunctionStorage, report.StatFunctionRegistryReadinessReport.Status);
		Assert.Equal(SkillBuffStat2EvaluationReadinessStatus.BlockedMissingStat2StateProvider, report.Stat2EvaluationReadinessReport.Status);
		Assert.Equal(SkillBuffStatCapRecalculationReadinessStatus.BlockedMissingCalculateBaseValueProvider, report.StatCapRecalculationReadinessReport.Status);
	}

	[Fact]
	public void CreateReport_AttachesDropBoostStatFunctionPlansAsEvidence()
	{
		var templates = new SkillTemplateTable(
		[
			CreateTemplate(
				8472,
				[new SkillBuffStatEffectSummary("boostdroprate", [new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 5)])]),
			CreateTemplate(
				9878,
				[new SkillBuffStatEffectSummary("drboost", [new SkillStatChange("DR_BOOST", "REPLACE", 80, 10)])])
		]);

		var report = WorldNpcDropBoostActiveStatProviderReadinessReportService.CreateReport(
			templates,
			statFunctionPlanSkillLevel: 2);

		Assert.Equal(WorldNpcDropBoostActiveStatProviderReadinessStatus.BlockedMissingActiveEffectControllerProvider, report.Status);
		var dropBoostPlan = Assert.Single(report.StatFunctionPlans, plan => string.Equals(plan.EffectName, "boostdroprate", StringComparison.Ordinal));
		var dropBoostFunction = Assert.Single(dropBoostPlan.Functions);
		Assert.Equal(8472, dropBoostPlan.SkillId);
		Assert.Equal(2, dropBoostPlan.SkillLevel);
		Assert.Equal("StatAddFunction", dropBoostFunction.JavaFunctionType);
		Assert.Equal(30, dropBoostFunction.EffectiveValue);
		Assert.True(dropBoostFunction.RequiresStatFunctionProxy);

		var drBoostPlan = Assert.Single(report.StatFunctionPlans, plan => string.Equals(plan.EffectName, "drboost", StringComparison.Ordinal));
		var drBoostFunction = Assert.Single(drBoostPlan.Functions);
		Assert.Equal("StatSetFunction", drBoostFunction.JavaFunctionType);
		Assert.Equal(100, drBoostFunction.EffectiveValue);
		Assert.False(drBoostFunction.IsBonus);
		Assert.Equal(2, report.StatFunctionRegistryReadinessReport.StatBuckets.Count);
		Assert.Equal(["BOOST_DROP_RATE", "DR_BOOST"], report.Stat2EvaluationReadinessReport.StatNames);
		Assert.Equal(1, report.Stat2EvaluationReadinessReport.AddFunctionCount);
		Assert.Equal(1, report.Stat2EvaluationReadinessReport.SetFunctionCount);
		Assert.Equal(["BOOST_DROP_RATE", "DR_BOOST"], report.StatCapRecalculationReadinessReport.StatNames);
		Assert.True(report.StatCapRecalculationReadinessReport.RequiresMaxHpMpRecalculation);
	}

	[Fact]
	public void CreateReport_RequiresBothStaticDropBoostEffectKindsBeforeLiveProviders()
	{
		var templates = new SkillTemplateTable(
		[
			CreateTemplate(
				8472,
				[new SkillBuffStatEffectSummary("boostdroprate", [new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0)])])
		]);

		var report = WorldNpcDropBoostActiveStatProviderReadinessReportService.CreateReport(
			templates,
			hasLiveActiveEffectControllerProvider: true,
			hasLiveEffectStatOwnerProvider: true,
			hasLiveStatFunctionRegistryProvider: true,
			hasLiveCreatureGameStatsStatQueryProvider: true,
			hasLiveConditionValidatorProvider: true);

		Assert.Equal(WorldNpcDropBoostActiveStatProviderReadinessStatus.MissingStaticMetadata, report.Status);
		Assert.False(report.IsReadyForDropWorkflow);
		Assert.DoesNotContain("static boostdroprate BOOST_DROP_RATE metadata", report.MissingInputs);
		Assert.Contains("static drboost DR_BOOST metadata", report.MissingInputs);
		Assert.DoesNotContain("live EffectController active-effect provider", report.MissingInputs);
		Assert.DoesNotContain("live CreatureGameStats.getStat provider", report.MissingInputs);
	}

	[Fact]
	public void CreateReport_BlocksConditionedDropBoostChangesUntilConditionValidatorExists()
	{
		var report = WorldNpcDropBoostActiveStatProviderReadinessReportService.CreateReport(
			CreateConditionedDropBoostSkillTemplates(),
			hasLiveActiveEffectControllerProvider: true,
			hasLiveEffectStatOwnerProvider: true,
			hasLiveStatFunctionRegistryProvider: true,
			hasLiveCreatureGameStatsStatQueryProvider: true,
			hasLiveConcurrentStatFunctionStorage: true,
			hasLiveStatFunctionInsertionProvider: true,
			hasLiveStatFunctionRemovalProvider: true,
			hasLiveSortedStatFunctionSnapshotProvider: true,
			hasLiveStatsChangeRecalculationProvider: true,
			hasLiveStat2StateProvider: true,
			hasLiveCurrentValueFormulaProvider: true,
			hasLiveAdditionStatProvider: true,
			hasLiveReverseStatProvider: true,
			hasLiveStatFunctionApplyProvider: true,
			hasLiveStatCapProvider: true,
			hasLiveCalculateBaseValueProvider: true,
			hasLiveCreatureAwareCapProvider: true,
			hasLiveMaxHpMpRecalculationProvider: true);

		Assert.Equal(WorldNpcDropBoostActiveStatProviderReadinessStatus.BlockedMissingConditionValidatorProvider, report.Status);
		Assert.False(report.IsReadyForDropWorkflow);
		Assert.Equal(SkillStatChangeConditionReadinessStatus.BlockedMissingConditionValidators, report.ConditionReadinessReport.Status);
		Assert.Equal(SkillStatConditionPreviewCoverageStatus.PreviewEvaluable, report.ConditionPreviewCoverageReport.Status);
		Assert.Equal(2, report.ConditionPreviewCoverageReport.PreviewEvaluableChangeCount);
		Assert.Equal(2, report.ConditionReadinessReport.ConditionEntryCount);
		Assert.Contains("live Conditions.validate provider", report.MissingInputs);
		Assert.DoesNotContain("live EffectController active-effect provider", report.MissingInputs);
		Assert.DoesNotContain("live Effect StatOwner provider", report.MissingInputs);
		Assert.DoesNotContain("live CreatureGameStats stat-function registry", report.MissingInputs);
		Assert.DoesNotContain("live CreatureGameStats.getStat provider", report.MissingInputs);
		Assert.All(report.StatFunctionPlans, plan => Assert.Equal(SkillBuffStatFunctionRegistryPlanStatus.BlockedMissingConditionValidatorProvider, plan.Status));
	}

	[Fact]
	public void CreateReport_BlocksWhenBroadStatRegistryProviderExistsButRegistrySemanticsAreMissing()
	{
		var report = WorldNpcDropBoostActiveStatProviderReadinessReportService.CreateReport(
			CreateDropBoostSkillTemplates(),
			hasLiveActiveEffectControllerProvider: true,
			hasLiveEffectStatOwnerProvider: true,
			hasLiveStatFunctionRegistryProvider: true,
			hasLiveCreatureGameStatsStatQueryProvider: true,
			hasLiveConditionValidatorProvider: true);

		Assert.Equal(WorldNpcDropBoostActiveStatProviderReadinessStatus.BlockedMissingStatFunctionRegistryReadiness, report.Status);
		Assert.False(report.IsReadyForDropWorkflow);
		Assert.Equal(SkillBuffStatFunctionRegistryReadinessStatus.BlockedMissingConcurrentStatFunctionStorage, report.StatFunctionRegistryReadinessReport.Status);
		Assert.Contains("live ConcurrentHashMap<StatEnum, List<IStatFunction>> equivalent", report.MissingInputs);
		Assert.Contains("live CreatureGameStats.addEffectOnly insertion provider", report.MissingInputs);
		Assert.Contains("live CreatureGameStats.endEffect removal provider", report.MissingInputs);
		Assert.Contains("live CreatureGameStats.getStatsSorted snapshot provider", report.MissingInputs);
		Assert.Contains("live CreatureGameStats.onStatsChange recalculation provider", report.MissingInputs);
	}

	[Fact]
	public void CreateReport_BlocksWhenRegistryIsReadyButStat2RuntimeEvaluationIsMissing()
	{
		var report = WorldNpcDropBoostActiveStatProviderReadinessReportService.CreateReport(
			CreateDropBoostSkillTemplates(),
			hasLiveActiveEffectControllerProvider: true,
			hasLiveEffectStatOwnerProvider: true,
			hasLiveStatFunctionRegistryProvider: true,
			hasLiveCreatureGameStatsStatQueryProvider: true,
			hasLiveConditionValidatorProvider: true,
			hasLiveConcurrentStatFunctionStorage: true,
			hasLiveStatFunctionInsertionProvider: true,
			hasLiveStatFunctionRemovalProvider: true,
			hasLiveSortedStatFunctionSnapshotProvider: true,
			hasLiveStatsChangeRecalculationProvider: true);

		Assert.Equal(WorldNpcDropBoostActiveStatProviderReadinessStatus.BlockedMissingStat2EvaluationReadiness, report.Status);
		Assert.False(report.IsReadyForDropWorkflow);
		Assert.Equal(SkillBuffStatFunctionRegistryReadinessStatus.Ready, report.StatFunctionRegistryReadinessReport.Status);
		Assert.Equal(SkillBuffStat2EvaluationReadinessStatus.BlockedMissingStat2StateProvider, report.Stat2EvaluationReadinessReport.Status);
		Assert.Contains("live Stat2 base/bonus/baseRate/bonusRate/fixedBonusRate state provider", report.MissingInputs);
		Assert.Contains("live Stat2.getCurrent/getExactCurrent formula provider", report.MissingInputs);
		Assert.Contains("live AdditionStat addToBase/addToBonus/calculatePercent provider", report.MissingInputs);
		Assert.Contains("live ReverseStat addToBase/addToBonus/calculatePercent provider", report.MissingInputs);
		Assert.Contains("live StatAddFunction/StatRateFunction/StatSetFunction apply provider", report.MissingInputs);
		Assert.Contains("live StatCapUtil.calculateBaseValue provider", report.MissingInputs);
	}

	[Fact]
	public void CreateReport_BlocksWhenStat2EvaluationIsReadyButStatCapRecalculationIsMissing()
	{
		var report = WorldNpcDropBoostActiveStatProviderReadinessReportService.CreateReport(
			CreateDropBoostSkillTemplates(),
			hasLiveActiveEffectControllerProvider: true,
			hasLiveEffectStatOwnerProvider: true,
			hasLiveStatFunctionRegistryProvider: true,
			hasLiveCreatureGameStatsStatQueryProvider: true,
			hasLiveConditionValidatorProvider: true,
			hasLiveConcurrentStatFunctionStorage: true,
			hasLiveStatFunctionInsertionProvider: true,
			hasLiveStatFunctionRemovalProvider: true,
			hasLiveSortedStatFunctionSnapshotProvider: true,
			hasLiveStatsChangeRecalculationProvider: true,
			hasLiveStat2StateProvider: true,
			hasLiveCurrentValueFormulaProvider: true,
			hasLiveAdditionStatProvider: true,
			hasLiveReverseStatProvider: true,
			hasLiveStatFunctionApplyProvider: true,
			hasLiveStatCapProvider: true);

		Assert.Equal(WorldNpcDropBoostActiveStatProviderReadinessStatus.BlockedMissingStatCapRecalculationReadiness, report.Status);
		Assert.False(report.IsReadyForDropWorkflow);
		Assert.Equal(SkillBuffStat2EvaluationReadinessStatus.Ready, report.Stat2EvaluationReadinessReport.Status);
		Assert.Equal(SkillBuffStatCapRecalculationReadinessStatus.BlockedMissingCalculateBaseValueProvider, report.StatCapRecalculationReadinessReport.Status);
		Assert.Contains("live StatCapUtil.calculateBaseValue provider", report.MissingInputs);
		Assert.Contains("live StatCapUtil creature-aware lower/upper cap provider", report.MissingInputs);
		Assert.Contains("live CreatureGameStats.onStatsChange max HP/MP recalculation provider", report.MissingInputs);
	}

	[Fact]
	public void CreateReport_ReportsUnsupportedStatFunctionPlansBeforeLiveProviderReadiness()
	{
		var templates = new SkillTemplateTable(
		[
			CreateTemplate(
				8472,
				[new SkillBuffStatEffectSummary("boostdroprate", [new SkillStatChange("BOOST_DROP_RATE", "ABS", 20, 0)])]),
			CreateTemplate(
				9878,
				[new SkillBuffStatEffectSummary("drboost", [new SkillStatChange("DR_BOOST", "ADD", 100, 0)])])
		]);

		var report = WorldNpcDropBoostActiveStatProviderReadinessReportService.CreateReport(templates);

		Assert.Equal(WorldNpcDropBoostActiveStatProviderReadinessStatus.UnsupportedStatFunctionPlan, report.Status);
		Assert.False(report.IsReadyForDropWorkflow);
		Assert.Contains("supported BufEffect stat function mapping", report.MissingInputs);
		var unsupportedPlan = Assert.Single(report.StatFunctionPlans, plan => plan.Status == SkillBuffStatFunctionRegistryPlanStatus.UnsupportedFunction);
		Assert.Equal("boostdroprate", unsupportedPlan.EffectName);
		Assert.Equal("unsupported", Assert.Single(unsupportedPlan.Functions).JavaFunctionType);
	}

	[Fact]
	public void CreateReport_BlocksUnsupportedConditionPreviewCoverageBeforeLiveReadiness()
	{
		var boostChange = new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0);
		boostChange.AddCondition(new SkillStatChangeConditionSummary("unsupported_condition", new Dictionary<string, string>(StringComparer.Ordinal)));

		var drBoostChange = new SkillStatChange("DR_BOOST", "ADD", 100, 0);
		drBoostChange.AddCondition(new SkillStatChangeConditionSummary("front", new Dictionary<string, string>(StringComparer.Ordinal)));

		var templates = new SkillTemplateTable(
		[
			CreateTemplate(8472, [new SkillBuffStatEffectSummary("boostdroprate", [boostChange])]),
			CreateTemplate(9878, [new SkillBuffStatEffectSummary("drboost", [drBoostChange])])
		]);

		var report = WorldNpcDropBoostActiveStatProviderReadinessReportService.CreateReport(templates);

		Assert.Equal(WorldNpcDropBoostActiveStatProviderReadinessStatus.BlockedUnsupportedConditionPreviewCoverage, report.Status);
		Assert.Equal(SkillStatConditionPreviewCoverageStatus.BlockedUnsupportedConditions, report.ConditionPreviewCoverageReport.Status);
		Assert.Contains("supported isolated stat-condition preview coverage", report.MissingInputs);
		Assert.Contains("unsupported isolated stat-condition evaluator: unsupported_condition", report.MissingInputs);
	}

	[Fact]
	public void CreateReport_BlocksBadStaticConditionAttributesBeforeLiveReadiness()
	{
		var boostChange = new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0);
		boostChange.AddCondition(new SkillStatChangeConditionSummary("weapon", new Dictionary<string, string>(StringComparer.Ordinal)));

		var drBoostChange = new SkillStatChange("DR_BOOST", "ADD", 100, 0);
		drBoostChange.AddCondition(new SkillStatChangeConditionSummary("front", new Dictionary<string, string>(StringComparer.Ordinal)));

		var templates = new SkillTemplateTable(
		[
			CreateTemplate(8472, [new SkillBuffStatEffectSummary("boostdroprate", [boostChange])]),
			CreateTemplate(9878, [new SkillBuffStatEffectSummary("drboost", [drBoostChange])])
		]);

		var report = WorldNpcDropBoostActiveStatProviderReadinessReportService.CreateReport(templates);

		Assert.Equal(WorldNpcDropBoostActiveStatProviderReadinessStatus.BlockedStaticConditionPreviewMetadata, report.Status);
		Assert.Equal(SkillStatConditionPreviewCoverageStatus.BlockedStaticMetadata, report.ConditionPreviewCoverageReport.Status);
		Assert.Contains("XML weapon attribute", report.MissingInputs);
	}

	[Fact]
	public void CreateReport_IsReadyOnlyWhenStaticMetadataAndEveryLiveProviderArePresent()
	{
		var report = WorldNpcDropBoostActiveStatProviderReadinessReportService.CreateReport(
			CreateConditionedDropBoostSkillTemplates(),
			hasLiveActiveEffectControllerProvider: true,
			hasLiveEffectStatOwnerProvider: true,
			hasLiveStatFunctionRegistryProvider: true,
			hasLiveCreatureGameStatsStatQueryProvider: true,
			hasLiveConditionValidatorProvider: true,
			hasLiveConcurrentStatFunctionStorage: true,
			hasLiveStatFunctionInsertionProvider: true,
			hasLiveStatFunctionRemovalProvider: true,
			hasLiveSortedStatFunctionSnapshotProvider: true,
			hasLiveStatsChangeRecalculationProvider: true,
			hasLiveStat2StateProvider: true,
			hasLiveCurrentValueFormulaProvider: true,
			hasLiveAdditionStatProvider: true,
			hasLiveReverseStatProvider: true,
			hasLiveStatFunctionApplyProvider: true,
			hasLiveStatCapProvider: true,
			hasLiveCalculateBaseValueProvider: true,
			hasLiveCreatureAwareCapProvider: true,
			hasLiveMaxHpMpRecalculationProvider: true);

		Assert.Equal(WorldNpcDropBoostActiveStatProviderReadinessStatus.Ready, report.Status);
		Assert.True(report.IsReadyForDropWorkflow);
		Assert.Empty(report.MissingInputs);
		Assert.Equal(WorldNpcDropBoostStatProviderReadinessStatus.Ready, report.StaticMetadataReport.Status);
		Assert.Equal(SkillStatChangeConditionReadinessStatus.Ready, report.ConditionReadinessReport.Status);
		Assert.Equal(SkillStatConditionPreviewCoverageStatus.PreviewEvaluable, report.ConditionPreviewCoverageReport.Status);
		Assert.All(report.StatFunctionPlans, plan => Assert.Equal(SkillBuffStatFunctionRegistryPlanStatus.Ready, plan.Status));
		Assert.Equal(SkillBuffStatFunctionRegistryReadinessStatus.Ready, report.StatFunctionRegistryReadinessReport.Status);
		Assert.Equal(SkillBuffStat2EvaluationReadinessStatus.Ready, report.Stat2EvaluationReadinessReport.Status);
		Assert.Equal(SkillBuffStatCapRecalculationReadinessStatus.Ready, report.StatCapRecalculationReadinessReport.Status);
	}

	private static SkillTemplateTable CreateDropBoostSkillTemplates()
	{
		return new SkillTemplateTable(
		[
			CreateTemplate(
				8472,
				[new SkillBuffStatEffectSummary("boostdroprate", [new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0)])]),
			CreateTemplate(
				9878,
				[new SkillBuffStatEffectSummary("drboost", [new SkillStatChange("DR_BOOST", "ADD", 100, 0)])])
		]);
	}

	private static SkillTemplateTable CreateConditionedDropBoostSkillTemplates()
	{
		var boostChange = new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0);
		boostChange.AddCondition(new SkillStatChangeConditionSummary("weapon", new Dictionary<string, string>(StringComparer.Ordinal) { ["weapon"] = "ORB" }));

		var drBoostChange = new SkillStatChange("DR_BOOST", "ADD", 100, 0);
		drBoostChange.AddCondition(new SkillStatChangeConditionSummary("front", new Dictionary<string, string>(StringComparer.Ordinal)));

		return new SkillTemplateTable(
		[
			CreateTemplate(
				8472,
				[new SkillBuffStatEffectSummary("boostdroprate", [boostChange])]),
			CreateTemplate(
				9878,
				[new SkillBuffStatEffectSummary("drboost", [drBoostChange])])
		]);
	}

	private static SkillTemplateSummary CreateTemplate(int skillId, IReadOnlyList<SkillBuffStatEffectSummary> buffStatEffects)
	{
		return new SkillTemplateSummary(
			skillId,
			$"Skill {skillId}",
			0,
			1,
			string.Empty,
			string.Empty,
			"MAGICAL",
			"NONE",
			0,
			0,
			BuffStatEffectSummaries: buffStatEffects);
	}
}
