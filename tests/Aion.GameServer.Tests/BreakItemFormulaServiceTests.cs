using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BreakItemFormulaServiceTests
{
	// --- calculateEffectiveLevel ---

	[Theory]
	[InlineData("RARE",   20, 25)]
	[InlineData("COMMON", 20, 25)]  // same as RARE
	[InlineData("LEGEND", 40, 50)]
	[InlineData("UNIQUE", 55, 70)]
	[InlineData("EPIC",   60, 80)]
	[InlineData("MYTHIC", 65, 90)]
	public void CalculateEffectiveLevel_ReturnsJavaValues(string quality, int level, int expected)
	{
		Assert.Equal(expected, BreakItemFormulaService.CalculateEffectiveLevel(quality, level));
	}

	// --- stone thresholds ---

	[Fact]
	public void Thresholds_MatchEnchantmentStoneEffectiveLevels()
	{
		Assert.Equal(25, BreakItemFormulaService.AlphaThreshold);
		Assert.Equal(50, BreakItemFormulaService.BetaThreshold);
		Assert.Equal(70, BreakItemFormulaService.GammaThreshold);
		Assert.Equal(80, BreakItemFormulaService.DeltaThreshold);
		Assert.Equal(90, BreakItemFormulaService.EpsilonThreshold);
	}

	// --- stone selection ---

	[Fact]
	public void CreatePlan_AlphaQualityLowLevelGivesAlphaStone()
	{
		// RARE item level 10 → effectiveLevel=15; +rnd0=15+weapon+5=20 < 25 → ALPHA
		var plan = BreakItemFormulaService.CreatePlan("RARE", 10, isWeapon: false, randomBonus: 0);

		Assert.Equal(166000191, plan.ResultStoneId);  // ALPHA
		Assert.Equal("ALPHA", plan.ResultStoneName);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_EpicHighLevelGivesDeltaStone()
	{
		// EPIC level 60 → effectiveLevel=80; +rnd0=80 >= 80 → DELTA (not EPSILON since <90)
		var plan = BreakItemFormulaService.CreatePlan("EPIC", 60, isWeapon: false, randomBonus: 0);

		Assert.Equal(166000194, plan.ResultStoneId);  // DELTA
		Assert.Equal("DELTA", plan.ResultStoneName);
	}

	[Fact]
	public void CreatePlan_MythicTopLevelGivesEpsilonStone()
	{
		// MYTHIC level 65 → effectiveLevel=90 >= 90 → EPSILON
		var plan = BreakItemFormulaService.CreatePlan("MYTHIC", 65, isWeapon: false, randomBonus: 0);

		Assert.Equal(166000195, plan.ResultStoneId);  // EPSILON
		Assert.Equal("EPSILON", plan.ResultStoneName);
	}

	[Fact]
	public void CreatePlan_WeaponAdds5ToEffectiveLevel()
	{
		// LEGEND 40 → base=50; weapon +5 = 55; +rnd5 = 60; 60 < 70 → BETA; >=50 → BETA
		var plan = BreakItemFormulaService.CreatePlan("LEGEND", 40, isWeapon: true, randomBonus: 5);

		Assert.Equal(60, plan.FinalEffectiveLevel);
		Assert.Equal("BETA", plan.ResultStoneName);
	}

	[Fact]
	public void CreatePlan_WeaponRewardCountIs2To5()
	{
		var plan = BreakItemFormulaService.CreatePlan("RARE", 50, isWeapon: true, randomBonus: 0);

		Assert.Equal(2, plan.MinRewardCount);
		Assert.Equal(5, plan.MaxRewardCount);
	}

	[Fact]
	public void CreatePlan_ArmorRewardCountIs1To3()
	{
		var plan = BreakItemFormulaService.CreatePlan("RARE", 50, isWeapon: false, randomBonus: 0);

		Assert.Equal(1, plan.MinRewardCount);
		Assert.Equal(3, plan.MaxRewardCount);
	}
}
