using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class CraftingXpFormulaServiceTests
{
	[Theory]
	// skillLvl=0: floor(0.008 * 100^2 + 60) = floor(80+60) = 140
	[InlineData(0,   140)]
	// skillLvl=100: floor(0.008 * 200^2 + 60) = floor(320+60) = 380
	[InlineData(100, 380)]
	// skillLvl=299: floor(0.008 * 399^2 + 60) = floor(0.008*159201+60) = floor(1273.608+60) = 1333
	[InlineData(299, 1333)]
	// skillLvl=449: floor(0.008 * 549^2 + 60) = floor(0.008*301401+60) = floor(2411.208+60) = 2471
	[InlineData(449, 2471)]
	public void CalculateBaseXp_MatchesJavaFormula(int skillPoint, int expectedBase)
	{
		Assert.Equal(expectedBase, CraftingXpFormulaService.CalculateBaseXp(skillPoint));
	}

	[Fact]
	public void CreatePlan_NoBonusReturnsBaseOnly()
	{
		var plan = CraftingXpFormulaService.CreatePlan(skillPoint: 0, bonusPercent: 0);

		Assert.Equal(140, plan.BaseXpReward);
		Assert.Equal(0, plan.BonusXp);
		Assert.Equal(140, plan.TotalXpReward);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_BonusAddsPercentage()
	{
		// skillLvl=0 → base=140; bonus=50% → +70; total=210
		var plan = CraftingXpFormulaService.CreatePlan(skillPoint: 0, bonusPercent: 50);

		Assert.Equal(140, plan.BaseXpReward);
		Assert.Equal(70, plan.BonusXp);
		Assert.Equal(210, plan.TotalXpReward);
	}

	[Fact]
	public void CreatePlan_100PercentBonusDoubles()
	{
		var plan = CraftingXpFormulaService.CreatePlan(skillPoint: 100, bonusPercent: 100);

		Assert.Equal(380, plan.BaseXpReward);
		Assert.Equal(380, plan.BonusXp);
		Assert.Equal(760, plan.TotalXpReward);
	}

	[Fact]
	public void CalculateBaseXp_IncreasesWithSkillLevel()
	{
		var low = CraftingXpFormulaService.CalculateBaseXp(0);
		var mid = CraftingXpFormulaService.CalculateBaseXp(200);
		var high = CraftingXpFormulaService.CalculateBaseXp(449);

		Assert.True(low < mid);
		Assert.True(mid < high);
	}
}
