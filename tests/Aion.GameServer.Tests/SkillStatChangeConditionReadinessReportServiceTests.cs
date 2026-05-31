using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SkillStatChangeConditionReadinessReportServiceTests
{
	[Fact]
	public void CreateReport_RecordsMissingSkillTemplates()
	{
		var report = SkillStatChangeConditionReadinessReportService.CreateReport(null);

		Assert.Equal(SkillStatChangeConditionReadinessStatus.MissingSkillTemplates, report.Status);
		Assert.False(report.IsReadyForConditionedStatChanges);
		Assert.Contains("skill_templates", report.MissingInputs);
		Assert.Contains("Conditions.validate", report.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateReport_ReportsNoConditionMetadataWhenChangesAreUnconditioned()
	{
		var report = SkillStatChangeConditionReadinessReportService.CreateReport(new SkillTemplateTable(
		[
			CreateTemplate(
				8472,
				[new SkillBuffStatEffectSummary("boostdroprate", [new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0)])])
		]));

		Assert.Equal(SkillStatChangeConditionReadinessStatus.NoConditionMetadata, report.Status);
		Assert.False(report.IsReadyForConditionedStatChanges);
		Assert.Equal(0, report.ConditionedChangeCount);
		Assert.Equal(0, report.ConditionEntryCount);
		Assert.Empty(report.ConditionNameCounts);
		Assert.Empty(report.MissingInputs);
	}

	[Fact]
	public void CreateReport_BlocksConditionedChangesUntilValidatorProviderExists()
	{
		var report = SkillStatChangeConditionReadinessReportService.CreateReport(CreateConditionedSkillTemplates());

		Assert.Equal(SkillStatChangeConditionReadinessStatus.BlockedMissingConditionValidators, report.Status);
		Assert.False(report.IsReadyForConditionedStatChanges);
		Assert.Equal(2, report.ConditionedChangeCount);
		Assert.Equal(3, report.ConditionEntryCount);
		Assert.Equal(
			[
				new SkillStatChangeConditionNameCount("front", 1),
				new SkillStatChangeConditionNameCount("weapon", 2)
			],
			report.ConditionNameCounts);
		Assert.Contains("live Conditions.validate provider", report.MissingInputs);
	}

	[Fact]
	public void CreateReport_IsReadyOnlyWhenConditionMetadataAndValidatorProviderArePresent()
	{
		var report = SkillStatChangeConditionReadinessReportService.CreateReport(
			CreateConditionedSkillTemplates(),
			hasLiveConditionValidatorProvider: true);

		Assert.Equal(SkillStatChangeConditionReadinessStatus.Ready, report.Status);
		Assert.True(report.IsReadyForConditionedStatChanges);
		Assert.Empty(report.MissingInputs);
	}

	private static SkillTemplateTable CreateConditionedSkillTemplates()
	{
		var boostChange = new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0);
		boostChange.AddCondition(new SkillStatChangeConditionSummary("weapon", new Dictionary<string, string>(StringComparer.Ordinal) { ["weapon"] = "ORB" }));
		boostChange.AddCondition(new SkillStatChangeConditionSummary("front", new Dictionary<string, string>(StringComparer.Ordinal)));

		var drBoostChange = new SkillStatChange("DR_BOOST", "ADD", 100, 0);
		drBoostChange.AddCondition(new SkillStatChangeConditionSummary("weapon", new Dictionary<string, string>(StringComparer.Ordinal) { ["weapon"] = "BOOK" }));

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
