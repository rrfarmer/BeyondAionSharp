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
		Assert.Contains("StatFunction.validate", report.JavaSource, StringComparison.Ordinal);
		Assert.Contains("before IStatFunction.apply", report.ValidateBeforeApplyRule, StringComparison.Ordinal);
		Assert.Contains("first failed child", report.ConditionShortCircuitRule, StringComparison.Ordinal);
		Assert.Contains("without applying", report.FailedValidationApplyRule, StringComparison.Ordinal);
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
		Assert.Empty(report.ValidatorPlans);
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
		Assert.Equal(["front", "weapon"], report.ValidatorPlans.Select(plan => plan.ConditionName));
		Assert.Equal(["FrontCondition", "WeaponCondition"], report.ValidatorPlans.Select(plan => plan.JavaConditionType));
		Assert.Equal([1, 2], report.ValidatorPlans.Select(plan => plan.EntryCount));
		var frontPlan = report.ValidatorPlans.Single(plan => plan.ConditionName == "front");
		Assert.Contains("does not override validate(Stat2, IStatFunction)", frontPlan.StatValidationBehavior, StringComparison.Ordinal);
		Assert.Contains("returns true", frontPlan.StatValidationBehavior, StringComparison.Ordinal);
		Assert.Contains("Condition base-class Stat2 validation pass-through", frontPlan.RequiredLiveInputs);
		Assert.Contains("Condition.validate(Stat2, IStatFunction) base method returns true", frontPlan.JavaSource, StringComparison.Ordinal);

		var weaponPlan = report.ValidatorPlans.Single(plan => plan.ConditionName == "weapon");
		Assert.Contains("Player equipment main-hand weapon ItemGroup", weaponPlan.RequiredLiveInputs);
		Assert.Contains("XML weapon attribute ItemGroup list", weaponPlan.RequiredLiveInputs);
		Assert.Contains("NPC owner pass-through rule", weaponPlan.RequiredLiveInputs);
		Assert.Contains("non-player owners return true", weaponPlan.JavaSource, StringComparison.Ordinal);
		Assert.All(report.ValidatorPlans, plan =>
		{
			Assert.Equal(SkillStatChangeConditionValidatorPlanStatus.BlockedMissingConditionValidatorProvider, plan.Status);
			Assert.True(plan.HasJavaConditionMapping);
			Assert.False(plan.HasLiveConditionValidatorProvider);
			Assert.Contains("before apply", plan.ValidateBeforeApplyRule, StringComparison.Ordinal);
			Assert.Contains("first child", plan.ConditionShortCircuitRule, StringComparison.Ordinal);
			Assert.Contains("live Conditions.validate provider", plan.MissingInputs);
			Assert.Contains("Conditions.validate", plan.JavaSource, StringComparison.Ordinal);
		});
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
		Assert.All(report.ValidatorPlans, plan => Assert.True(plan.IsReadyForValidation));
		Assert.Equal([SkillStatChangeConditionValidatorPlanStatus.Ready, SkillStatChangeConditionValidatorPlanStatus.Ready], report.ValidatorPlans.Select(plan => plan.Status));
	}

	[Fact]
	public void CreateReport_ClassifiesMappedStatConditionOverrideAndPassThroughBehavior()
	{
		var itemChargeChange = new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0);
		itemChargeChange.AddCondition(new SkillStatChangeConditionSummary("charge", new Dictionary<string, string>(StringComparer.Ordinal) { ["value"] = "1" }));

		var onFlyChange = new SkillStatChange("DR_BOOST", "ADD", 20, 0);
		onFlyChange.AddCondition(new SkillStatChangeConditionSummary("onfly", new Dictionary<string, string>(StringComparer.Ordinal)));

		var passThroughChange = new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0);
		passThroughChange.AddCondition(new SkillStatChangeConditionSummary("back", new Dictionary<string, string>(StringComparer.Ordinal)));
		passThroughChange.AddCondition(new SkillStatChangeConditionSummary("chargeweapon", new Dictionary<string, string>(StringComparer.Ordinal) { ["value"] = "1" }));

		var report = SkillStatChangeConditionReadinessReportService.CreateReport(new SkillTemplateTable(
		[
			CreateTemplate(2001, [new SkillBuffStatEffectSummary("boostdroprate", [itemChargeChange, passThroughChange])]),
			CreateTemplate(2002, [new SkillBuffStatEffectSummary("drboost", [onFlyChange])])
		]));

		var chargePlan = report.ValidatorPlans.Single(plan => plan.ConditionName == "charge");
		Assert.Contains("statFunction.getOwner()", chargePlan.StatValidationBehavior, StringComparison.Ordinal);
		Assert.Contains("Item charge level", chargePlan.RequiredLiveInputs);
		Assert.Contains("non-Item owners return false", chargePlan.JavaSource, StringComparison.Ordinal);

		var onFlyPlan = report.ValidatorPlans.Single(plan => plan.ConditionName == "onfly");
		Assert.Contains("stat.getOwner().isFlying()", onFlyPlan.StatValidationBehavior, StringComparison.Ordinal);
		Assert.Contains("Creature flying state", onFlyPlan.RequiredLiveInputs);
		Assert.Contains("OnFlyCondition.validate(Stat2, IStatFunction)", onFlyPlan.JavaSource, StringComparison.Ordinal);

		var backPlan = report.ValidatorPlans.Single(plan => plan.ConditionName == "back");
		Assert.Contains("does not override validate(Stat2, IStatFunction)", backPlan.StatValidationBehavior, StringComparison.Ordinal);
		Assert.Contains("Condition base-class Stat2 validation pass-through", backPlan.RequiredLiveInputs);
		Assert.Contains("Condition.validate base method returns true", backPlan.JavaSource, StringComparison.Ordinal);

		var chargeWeaponPlan = report.ValidatorPlans.Single(plan => plan.ConditionName == "chargeweapon");
		Assert.Contains("does not override validate(Stat2, IStatFunction)", chargeWeaponPlan.StatValidationBehavior, StringComparison.Ordinal);
		Assert.Contains(chargeWeaponPlan.RequiredLiveInputs, input => input.Contains("ChargeWeaponCondition Skill/Effect validation remains separate", StringComparison.Ordinal));
		Assert.Contains("Condition.validate base method returns true", chargeWeaponPlan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateReport_BlocksUnknownConditionMetadataBeforeValidatorProviderReadiness()
	{
		var change = new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0);
		change.AddCondition(new SkillStatChangeConditionSummary("unsupported_condition", new Dictionary<string, string>(StringComparer.Ordinal)));
		var report = SkillStatChangeConditionReadinessReportService.CreateReport(
			new SkillTemplateTable(
			[
				CreateTemplate(8472, [new SkillBuffStatEffectSummary("boostdroprate", [change])])
			]),
			hasLiveConditionValidatorProvider: true);

		Assert.Equal(SkillStatChangeConditionReadinessStatus.UnsupportedConditionMetadata, report.Status);
		Assert.False(report.IsReadyForConditionedStatChanges);
		var plan = Assert.Single(report.ValidatorPlans);
		Assert.Equal(SkillStatChangeConditionValidatorPlanStatus.UnsupportedConditionMetadata, plan.Status);
		Assert.Equal("unsupported", plan.JavaConditionType);
		Assert.False(plan.HasJavaConditionMapping);
		Assert.Contains("supported Java Conditions child mapping", report.MissingInputs);
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
