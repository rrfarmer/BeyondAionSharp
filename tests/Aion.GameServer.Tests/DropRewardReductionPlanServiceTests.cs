using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class DropRewardReductionPlanServiceTests
{
	// --- DropRewardFrom ---

	[Theory]
	[InlineData(-10, 0)]
	[InlineData(-11, 0)]
	[InlineData(-20, 0)]
	public void DropRewardFrom_LevelDiffAtOrBelow_10_IsZero(int levelDiff, int expectedPercent)
	{
		Assert.Equal(expectedPercent, DropRewardReductionPlanService.DropRewardFrom(levelDiff));
	}

	[Theory]
	[InlineData(-9, 40)]
	[InlineData(-8, 60)]
	[InlineData(-7, 70)]
	[InlineData(-6, 80)]
	public void DropRewardFrom_SpecificLevelDiffs(int levelDiff, int expectedPercent)
	{
		Assert.Equal(expectedPercent, DropRewardReductionPlanService.DropRewardFrom(levelDiff));
	}

	[Theory]
	[InlineData(-5, 100)]
	[InlineData(0, 100)]
	[InlineData(5, 100)]
	[InlineData(-4, 100)]
	public void DropRewardFrom_LevelDiffAtOrAbove_Minus5_Is100(int levelDiff, int expectedPercent)
	{
		Assert.Equal(expectedPercent, DropRewardReductionPlanService.DropRewardFrom(levelDiff));
	}

	// --- getReductionDropRate via CreatePlan ---

	[Fact]
	public void CreatePlan_100PercentHasNoReduction()
	{
		// npcLevel == highestPlayerLevel → diff = 0 → 100% → no reduction
		var plan = DropRewardReductionPlanService.CreatePlan(npcLevel: 30, highestPlayerLevel: 30);

		Assert.False(plan.HasReduction);
		Assert.Null(plan.ReductionRate);
		Assert.Equal(100, plan.DropRewardPercent);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_NpcMuchWeakerThanPlayerHasMaxReduction()
	{
		// npc level 20, player level 35 → diff = -15 → 0% → reduction = 0.0f
		var plan = DropRewardReductionPlanService.CreatePlan(npcLevel: 20, highestPlayerLevel: 35);

		Assert.True(plan.HasReduction);
		Assert.Equal(0, plan.DropRewardPercent);
		Assert.Equal(0f, plan.ReductionRate);
	}

	[Fact]
	public void CreatePlan_NpcSixLevelsLowerHas80PercentRate()
	{
		// diff = -6 → 80% → rate = 0.8f
		var plan = DropRewardReductionPlanService.CreatePlan(npcLevel: 24, highestPlayerLevel: 30);

		Assert.True(plan.HasReduction);
		Assert.Equal(80, plan.DropRewardPercent);
		Assert.NotNull(plan.ReductionRate);
		Assert.Equal(0.8f, plan.ReductionRate!.Value, 6);
	}
}
