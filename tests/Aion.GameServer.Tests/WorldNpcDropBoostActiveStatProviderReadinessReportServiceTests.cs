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
	}

	[Fact]
	public void CreateReport_BlocksAfterStaticMetadataWhenActiveEffectProviderIsMissing()
	{
		var report = WorldNpcDropBoostActiveStatProviderReadinessReportService.CreateReport(CreateDropBoostSkillTemplates());

		Assert.Equal(WorldNpcDropBoostActiveStatProviderReadinessStatus.BlockedMissingActiveEffectControllerProvider, report.Status);
		Assert.False(report.IsReadyForDropWorkflow);
		Assert.Equal(WorldNpcDropBoostStatProviderReadinessStatus.BlockedMissingLiveEffectStateProvider, report.StaticMetadataReport.Status);
		Assert.Equal(SkillStatChangeConditionReadinessStatus.NoConditionMetadata, report.ConditionReadinessReport.Status);
		Assert.DoesNotContain("static boostdroprate BOOST_DROP_RATE metadata", report.MissingInputs);
		Assert.DoesNotContain("static drboost DR_BOOST metadata", report.MissingInputs);
		Assert.Contains("live EffectController active-effect provider", report.MissingInputs);
		Assert.Contains("live Effect StatOwner provider", report.MissingInputs);
		Assert.Contains("live CreatureGameStats stat-function registry", report.MissingInputs);
		Assert.Contains("live CreatureGameStats.getStat provider", report.MissingInputs);
		Assert.DoesNotContain("live Conditions.validate provider", report.MissingInputs);
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
			hasLiveCreatureGameStatsStatQueryProvider: true);

		Assert.Equal(WorldNpcDropBoostActiveStatProviderReadinessStatus.BlockedMissingConditionValidatorProvider, report.Status);
		Assert.False(report.IsReadyForDropWorkflow);
		Assert.Equal(SkillStatChangeConditionReadinessStatus.BlockedMissingConditionValidators, report.ConditionReadinessReport.Status);
		Assert.Equal(2, report.ConditionReadinessReport.ConditionEntryCount);
		Assert.Contains("live Conditions.validate provider", report.MissingInputs);
		Assert.DoesNotContain("live EffectController active-effect provider", report.MissingInputs);
		Assert.DoesNotContain("live Effect StatOwner provider", report.MissingInputs);
		Assert.DoesNotContain("live CreatureGameStats stat-function registry", report.MissingInputs);
		Assert.DoesNotContain("live CreatureGameStats.getStat provider", report.MissingInputs);
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
			hasLiveConditionValidatorProvider: true);

		Assert.Equal(WorldNpcDropBoostActiveStatProviderReadinessStatus.Ready, report.Status);
		Assert.True(report.IsReadyForDropWorkflow);
		Assert.Empty(report.MissingInputs);
		Assert.Equal(WorldNpcDropBoostStatProviderReadinessStatus.Ready, report.StaticMetadataReport.Status);
		Assert.Equal(SkillStatChangeConditionReadinessStatus.Ready, report.ConditionReadinessReport.Status);
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
