using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SkillBuffStatFunctionRegistryReadinessReportServiceTests
{
	[Fact]
	public void CreateReport_RecordsNoFunctionPlans()
	{
		var report = SkillBuffStatFunctionRegistryReadinessReportService.CreateReport([]);

		Assert.Equal(SkillBuffStatFunctionRegistryReadinessStatus.NoFunctionPlans, report.Status);
		Assert.False(report.IsReadyForLiveRegistry);
		Assert.Equal(0, report.FunctionPlanCount);
		Assert.Equal(0, report.FunctionCount);
		Assert.Empty(report.StatBuckets);
		Assert.Contains("CreatureGameStats.addEffectOnly", report.JavaSource, StringComparison.Ordinal);
		Assert.Contains("live ConcurrentHashMap<StatEnum, List<IStatFunction>> equivalent", report.MissingInputs);
		Assert.Contains("live CreatureGameStats.addEffectOnly insertion provider", report.MissingInputs);
		Assert.Contains("live CreatureGameStats.endEffect removal provider", report.MissingInputs);
		Assert.Contains("live CreatureGameStats.getStatsSorted snapshot provider", report.MissingInputs);
		Assert.Contains("live CreatureGameStats.onStatsChange recalculation provider", report.MissingInputs);
	}

	[Fact]
	public void CreateReport_GroupsFunctionPlansByStatAndPreservesPriorityOrderEvidence()
	{
		var report = SkillBuffStatFunctionRegistryReadinessReportService.CreateReport(
		[
			CreatePlan(
				9878,
				"drboost",
				[
					new SkillStatChange("DR_BOOST", "ADD", 7, 0),
					new SkillStatChange("DR_BOOST", "REPLACE", 80, 0),
					new SkillStatChange("BOOST_DROP_RATE", "PERCENT", 50, 0)
				])
		]);

		Assert.Equal(SkillBuffStatFunctionRegistryReadinessStatus.BlockedMissingConcurrentStatFunctionStorage, report.Status);
		Assert.Equal(1, report.FunctionPlanCount);
		Assert.Equal(3, report.FunctionCount);
		Assert.Equal(3, report.RequiresProxyCount);
		Assert.Equal(0, report.ConditionedFunctionCount);

		var boostBucket = Assert.Single(report.StatBuckets, bucket => string.Equals(bucket.StatName, "BOOST_DROP_RATE", StringComparison.Ordinal));
		Assert.Equal(["StatRateFunction"], boostBucket.Functions.Select(function => function.JavaFunctionType));

		var drBucket = Assert.Single(report.StatBuckets, bucket => string.Equals(bucket.StatName, "DR_BOOST", StringComparison.Ordinal));
		Assert.Equal(["StatSetFunction", "StatAddFunction"], drBucket.Functions.Select(function => function.JavaFunctionType));
		Assert.Equal([40, 60], drBucket.Functions.Select(function => function.Priority));
	}

	[Fact]
	public void CreateReport_CountsConditionedFunctionsAndKeepsValidatorGapInNestedPlan()
	{
		var conditioned = new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0);
		conditioned.AddCondition(new SkillStatChangeConditionSummary("weapon", new Dictionary<string, string>(StringComparer.Ordinal) { ["weapon"] = "ORB" }));

		var report = SkillBuffStatFunctionRegistryReadinessReportService.CreateReport(
		[
			CreatePlan(8472, "boostdroprate", [conditioned], hasOwner: true, hasRegistry: true)
		],
			hasLiveConcurrentStatFunctionStorage: true,
			hasLiveStatFunctionInsertionProvider: true,
			hasLiveStatFunctionRemovalProvider: true,
			hasLiveSortedSnapshotProvider: true,
			hasLiveStatsChangeRecalculationProvider: true);

		Assert.Equal(SkillBuffStatFunctionRegistryReadinessStatus.Ready, report.Status);
		Assert.True(report.IsReadyForLiveRegistry);
		Assert.Equal(1, report.ConditionedFunctionCount);
		Assert.Empty(report.MissingInputs);
	}

	[Fact]
	public void CreateReport_BlocksUnsupportedFunctionPlanBeforeLiveProviders()
	{
		var report = SkillBuffStatFunctionRegistryReadinessReportService.CreateReport(
		[
			CreatePlan(
				8472,
				"boostdroprate",
				[new SkillStatChange("BOOST_DROP_RATE", "ABS", 20, 0)],
				hasOwner: true,
				hasRegistry: true,
				hasConditionValidator: true)
		],
			hasLiveConcurrentStatFunctionStorage: true,
			hasLiveStatFunctionInsertionProvider: true,
			hasLiveStatFunctionRemovalProvider: true,
			hasLiveSortedSnapshotProvider: true,
			hasLiveStatsChangeRecalculationProvider: true);

		Assert.Equal(SkillBuffStatFunctionRegistryReadinessStatus.UnsupportedFunctionPlan, report.Status);
		Assert.False(report.IsReadyForLiveRegistry);
		Assert.Contains("supported BufEffect stat function mapping", report.MissingInputs);
	}

	[Fact]
	public void CreateReport_RequiresEveryLiveRegistrySemanticGate()
	{
		var plans = new[]
		{
			CreatePlan(
				8472,
				"boostdroprate",
				[new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0)],
				hasOwner: true,
				hasRegistry: true,
				hasConditionValidator: true)
		};

		var missingInsertion = SkillBuffStatFunctionRegistryReadinessReportService.CreateReport(
			plans,
			hasLiveConcurrentStatFunctionStorage: true);
		Assert.Equal(SkillBuffStatFunctionRegistryReadinessStatus.BlockedMissingInsertionProvider, missingInsertion.Status);

		var missingRemoval = SkillBuffStatFunctionRegistryReadinessReportService.CreateReport(
			plans,
			hasLiveConcurrentStatFunctionStorage: true,
			hasLiveStatFunctionInsertionProvider: true);
		Assert.Equal(SkillBuffStatFunctionRegistryReadinessStatus.BlockedMissingRemovalProvider, missingRemoval.Status);

		var missingSnapshot = SkillBuffStatFunctionRegistryReadinessReportService.CreateReport(
			plans,
			hasLiveConcurrentStatFunctionStorage: true,
			hasLiveStatFunctionInsertionProvider: true,
			hasLiveStatFunctionRemovalProvider: true);
		Assert.Equal(SkillBuffStatFunctionRegistryReadinessStatus.BlockedMissingSortedSnapshotProvider, missingSnapshot.Status);

		var missingRecalculation = SkillBuffStatFunctionRegistryReadinessReportService.CreateReport(
			plans,
			hasLiveConcurrentStatFunctionStorage: true,
			hasLiveStatFunctionInsertionProvider: true,
			hasLiveStatFunctionRemovalProvider: true,
			hasLiveSortedSnapshotProvider: true);
		Assert.Equal(SkillBuffStatFunctionRegistryReadinessStatus.BlockedMissingStatsChangeRecalculationProvider, missingRecalculation.Status);
	}

	[Fact]
	public void CreateReport_IsReadyOnlyWhenEveryLiveRegistryGateExists()
	{
		var report = SkillBuffStatFunctionRegistryReadinessReportService.CreateReport(
		[
			CreatePlan(
				8472,
				"boostdroprate",
				[new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0)],
				hasOwner: true,
				hasRegistry: true,
				hasConditionValidator: true)
		],
			hasLiveConcurrentStatFunctionStorage: true,
			hasLiveStatFunctionInsertionProvider: true,
			hasLiveStatFunctionRemovalProvider: true,
			hasLiveSortedSnapshotProvider: true,
			hasLiveStatsChangeRecalculationProvider: true);

		Assert.Equal(SkillBuffStatFunctionRegistryReadinessStatus.Ready, report.Status);
		Assert.True(report.IsReadyForLiveRegistry);
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
