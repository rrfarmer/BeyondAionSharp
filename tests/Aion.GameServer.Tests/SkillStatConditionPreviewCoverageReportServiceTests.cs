using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SkillStatConditionPreviewCoverageReportServiceTests
{
	[Fact]
	public void CreateReport_RecordsMissingSkillTemplates()
	{
		var report = SkillStatConditionPreviewCoverageReportService.CreateReport(null);

		Assert.Equal(SkillStatConditionPreviewCoverageStatus.MissingSkillTemplates, report.Status);
		Assert.False(report.HasCompleteStaticPreviewCoverage);
		Assert.Contains("skill_templates", report.MissingInputs);
		Assert.Contains("BufEffect.getModifiers", report.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateReport_ReportsNoConditionedChanges()
	{
		var report = SkillStatConditionPreviewCoverageReportService.CreateReport(new SkillTemplateTable(
		[
			CreateTemplate(1, [new SkillBuffStatEffectSummary("boostdroprate", [new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0)])])
		]));

		Assert.Equal(SkillStatConditionPreviewCoverageStatus.NoConditionedChanges, report.Status);
		Assert.Equal(0, report.ConditionedChangeCount);
		Assert.Equal(0, report.ConditionEntryCount);
		Assert.Empty(report.Combinations);
		Assert.Empty(report.MissingInputs);
	}

	[Fact]
	public void CreateReport_EnumeratesPreviewEvaluableConditionCombinations()
	{
		var boostChange = new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0);
		boostChange.AddCondition(new SkillStatChangeConditionSummary("weapon", new Dictionary<string, string>(StringComparer.Ordinal) { ["weapon"] = "ORB SPELLBOOK" }));
		boostChange.AddCondition(new SkillStatChangeConditionSummary("front", new Dictionary<string, string>(StringComparer.Ordinal)));

		var drBoostChange = new SkillStatChange("DR_BOOST", "ADD", 100, 0);
		drBoostChange.AddCondition(new SkillStatChangeConditionSummary("onfly", new Dictionary<string, string>(StringComparer.Ordinal)));

		var report = SkillStatConditionPreviewCoverageReportService.CreateReport(new SkillTemplateTable(
		[
			CreateTemplate(8472, [new SkillBuffStatEffectSummary("boostdroprate", [boostChange])]),
			CreateTemplate(9878, [new SkillBuffStatEffectSummary("drboost", [drBoostChange])])
		]));

		Assert.Equal(SkillStatConditionPreviewCoverageStatus.PreviewEvaluable, report.Status);
		Assert.True(report.HasCompleteStaticPreviewCoverage);
		Assert.Equal(2, report.ConditionedChangeCount);
		Assert.Equal(3, report.ConditionEntryCount);
		Assert.Equal(2, report.PreviewEvaluableChangeCount);
		Assert.Equal(0, report.BlockedChangeCount);
		Assert.Empty(report.MissingInputs);

		var boostCombination = Assert.Single(report.Combinations, combination => combination.SkillId == 8472);
		Assert.Equal(SkillStatConditionPreviewCombinationStatus.PreviewEvaluable, boostCombination.Status);
		Assert.Equal("weapon -> front", boostCombination.ConditionSequence);
		Assert.Equal(["creature condition input snapshot", "player main-hand ItemGroup snapshot"], boostCombination.RequiredRuntimeInputs);
		Assert.Equal(["weapon", "front"], boostCombination.Conditions.Select(condition => condition.ConditionName).ToArray());
		Assert.All(boostCombination.Conditions, condition => Assert.Equal(SkillStatConditionPreviewConditionStatus.PreviewEvaluable, condition.Status));

		var drBoostCombination = Assert.Single(report.Combinations, combination => combination.SkillId == 9878);
		Assert.Equal(["creature condition input snapshot"], drBoostCombination.RequiredRuntimeInputs);
		Assert.Equal("onfly", Assert.Single(drBoostCombination.Conditions).ConditionName);
	}

	[Fact]
	public void CreateReport_BlocksInvalidStaticAttributesBeforePreviewClaims()
	{
		var missingWeapon = new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0);
		missingWeapon.AddCondition(new SkillStatChangeConditionSummary("weapon", new Dictionary<string, string>(StringComparer.Ordinal)));

		var invalidCharge = new SkillStatChange("DR_BOOST", "ADD", 100, 0);
		invalidCharge.AddCondition(new SkillStatChangeConditionSummary("charge", new Dictionary<string, string>(StringComparer.Ordinal) { ["value"] = "not-an-int" }));

		var report = SkillStatConditionPreviewCoverageReportService.CreateReport(new SkillTemplateTable(
		[
			CreateTemplate(8472, [new SkillBuffStatEffectSummary("boostdroprate", [missingWeapon])]),
			CreateTemplate(9878, [new SkillBuffStatEffectSummary("drboost", [invalidCharge])])
		]));

		Assert.Equal(SkillStatConditionPreviewCoverageStatus.BlockedStaticMetadata, report.Status);
		Assert.False(report.HasCompleteStaticPreviewCoverage);
		Assert.Equal(0, report.PreviewEvaluableChangeCount);
		Assert.Equal(2, report.BlockedChangeCount);
		Assert.Equal(["XML value attribute integer", "XML weapon attribute"], report.MissingInputs);
		Assert.All(report.Combinations, combination => Assert.Equal(SkillStatConditionPreviewCombinationStatus.BlockedStaticMetadata, combination.Status));
	}

	[Fact]
	public void CreateReport_BlocksUnsupportedConditionsBeforeStaticPreviewCoverage()
	{
		var unsupportedChange = new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0);
		unsupportedChange.AddCondition(new SkillStatChangeConditionSummary("unsupported_condition", new Dictionary<string, string>(StringComparer.Ordinal)));

		var report = SkillStatConditionPreviewCoverageReportService.CreateReport(new SkillTemplateTable(
		[
			CreateTemplate(8472, [new SkillBuffStatEffectSummary("boostdroprate", [unsupportedChange])])
		]));

		Assert.Equal(SkillStatConditionPreviewCoverageStatus.BlockedUnsupportedConditions, report.Status);
		Assert.Equal(1, report.BlockedChangeCount);
		Assert.Contains("unsupported isolated stat-condition evaluator: unsupported_condition", report.MissingInputs);

		var combination = Assert.Single(report.Combinations);
		Assert.Equal(SkillStatConditionPreviewCombinationStatus.BlockedUnsupportedCondition, combination.Status);
		var condition = Assert.Single(combination.Conditions);
		Assert.Equal(SkillStatConditionPreviewConditionStatus.UnsupportedCondition, condition.Status);
		Assert.Contains("Conditions.validate child mapping", condition.JavaSource, StringComparison.Ordinal);
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
