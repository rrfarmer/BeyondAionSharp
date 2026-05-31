using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SkillBuffStatCapRecalculationReadinessReportServiceTests
{
	[Fact]
	public void CreateReport_RecordsNoFunctionPlans()
	{
		var report = SkillBuffStatCapRecalculationReadinessReportService.CreateReport([]);

		Assert.Equal(SkillBuffStatCapRecalculationReadinessStatus.NoFunctionPlans, report.Status);
		Assert.False(report.IsReadyForStatCapRecalculation);
		Assert.Equal(0, report.FunctionPlanCount);
		Assert.Equal(0, report.FunctionCount);
		Assert.Empty(report.StatNames);
		Assert.False(report.RequiresMaxHpMpRecalculation);
		Assert.Contains("StatCapUtil.calculateBaseValue", report.JavaSource, StringComparison.Ordinal);
		Assert.Contains("CreatureGameStats.onStatsChange", report.JavaSource, StringComparison.Ordinal);
		Assert.Contains("live StatCapUtil.calculateBaseValue provider", report.MissingInputs);
		Assert.Contains("live StatCapUtil creature-aware lower/upper cap provider", report.MissingInputs);
	}

	[Fact]
	public void CreateReport_CapturesDropBoostStatCapEvidence()
	{
		var report = SkillBuffStatCapRecalculationReadinessReportService.CreateReport(
		[
			CreatePlan(
				9878,
				"drboost",
				[
					new SkillStatChange("DR_BOOST", "ADD", 100, 0),
					new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0)
				])
		]);

		Assert.Equal(SkillBuffStatCapRecalculationReadinessStatus.BlockedMissingCalculateBaseValueProvider, report.Status);
		Assert.Equal(["BOOST_DROP_RATE", "DR_BOOST"], report.StatNames);
		Assert.False(report.RequiresAttackSpeedBonusClamp);
		Assert.False(report.RequiresElementalDefenseCaps);
		Assert.False(report.RequiresSpeedUnrestrictedCap);
		Assert.True(report.RequiresMaxHpMpRecalculation);
		Assert.Contains("live CreatureGameStats.onStatsChange max HP/MP recalculation provider", report.MissingInputs);
	}

	[Fact]
	public void CreateReport_FlagsSpecialCapBranchesFromJavaStatCapUtil()
	{
		var report = SkillBuffStatCapRecalculationReadinessReportService.CreateReport(
		[
			CreatePlan(
				321,
				"speed",
				[
					new SkillStatChange("ATTACK_SPEED", "ADD", -100, 0),
					new SkillStatChange("FIRE_RESISTANCE", "ADD", 200, 0),
					new SkillStatChange("SPEED", "ADD", 1000, 0)
				],
				hasOwner: true,
				hasRegistry: true,
				hasConditionValidator: true)
		],
			hasLiveCalculateBaseValueProvider: true,
			hasLiveCreatureAwareCapProvider: true);

		Assert.Equal(SkillBuffStatCapRecalculationReadinessStatus.BlockedMissingAttackSpeedBonusClampProvider, report.Status);
		Assert.True(report.RequiresAttackSpeedBonusClamp);
		Assert.True(report.RequiresElementalDefenseCaps);
		Assert.True(report.RequiresSpeedUnrestrictedCap);
		Assert.Contains("live StatCapUtil ATTACK_SPEED bonus clamp provider", report.MissingInputs);
	}

	[Fact]
	public void CreateReport_BlocksUnsupportedFunctionPlansBeforeLiveProviders()
	{
		var report = SkillBuffStatCapRecalculationReadinessReportService.CreateReport(
		[
			CreatePlan(
				8472,
				"boostdroprate",
				[new SkillStatChange("BOOST_DROP_RATE", "ABS", 20, 0)],
				hasOwner: true,
				hasRegistry: true,
				hasConditionValidator: true)
		],
			hasLiveCalculateBaseValueProvider: true,
			hasLiveCreatureAwareCapProvider: true,
			hasLiveAttackSpeedBonusClampProvider: true,
			hasLiveMaxHpMpRecalculationProvider: true);

		Assert.Equal(SkillBuffStatCapRecalculationReadinessStatus.UnsupportedFunctionPlan, report.Status);
		Assert.False(report.IsReadyForStatCapRecalculation);
		Assert.Contains("supported BufEffect stat function mapping", report.MissingInputs);
	}

	[Fact]
	public void CreateReport_IsReadyOnlyWhenRequiredLiveProvidersExist()
	{
		var report = SkillBuffStatCapRecalculationReadinessReportService.CreateReport(
		[
			CreatePlan(
				8472,
				"boostdroprate",
				[new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0)],
				hasOwner: true,
				hasRegistry: true,
				hasConditionValidator: true)
		],
			hasLiveCalculateBaseValueProvider: true,
			hasLiveCreatureAwareCapProvider: true,
			hasLiveAttackSpeedBonusClampProvider: false,
			hasLiveMaxHpMpRecalculationProvider: true);

		Assert.Equal(SkillBuffStatCapRecalculationReadinessStatus.Ready, report.Status);
		Assert.True(report.IsReadyForStatCapRecalculation);
		Assert.Empty(report.MissingInputs);
	}

	private static SkillBuffStatFunctionRegistryPlan CreatePlan(
		int skillId,
		string effectName,
		IReadOnlyList<SkillStatChange> changes,
		bool hasOwner = false,
		bool hasRegistry = false,
		bool hasConditionValidator = false)
	{
		return SkillBuffStatFunctionPlanService.CreateRegistryPlan(
			skillId,
			effectName,
			1,
			changes,
			hasOwner,
			hasRegistry,
			hasConditionValidator);
	}
}
