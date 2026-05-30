using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class DropChanceFormulaServiceTests
{
	// --- getRankModifier ---

	[Theory]
	[InlineData("NOVICE", 0.9f)]
	[InlineData("DISCIPLINED", 1.0f)]
	[InlineData("SEASONED", 1.05f)]
	[InlineData("EXPERT", 1.1f)]
	[InlineData("VETERAN", 1.15f)]
	[InlineData("MASTER", 1.2f)]
	public void GetNpcRankModifier_ReturnsJavaValues(string rank, float expected)
	{
		Assert.Equal(expected, DropChanceFormulaService.GetNpcRankModifier(rank), 6);
	}

	// --- getRatingModifier ---

	[Theory]
	[InlineData("JUNK", 0.5f)]
	[InlineData("NORMAL", 1.0f)]
	[InlineData("ELITE", 1.3f)]
	[InlineData("HERO", 1.8f)]
	[InlineData("LEGENDARY", 2.0f)]
	public void GetNpcRatingModifier_ReturnsJavaValues(string rating, float expected)
	{
		Assert.Equal(expected, DropChanceFormulaService.GetNpcRatingModifier(rating), 6);
	}

	// --- calculateDropChance ---

	[Fact]
	public void CalculateDropChance_BoostOnlyNoReduction()
	{
		// chance=50, no reduction, boost=2.0 → 50 * 2.0 = 100
		var result = DropChanceFormulaService.CalculateDropChance(50f, false, null, 2.0f);
		Assert.Equal(100f, result, 6);
	}

	[Fact]
	public void CalculateDropChance_WithReductionAndBoost()
	{
		// chance=100, reduction=0.6, boost=2.0 → 100 * 0.6 = 60; 60 * 2.0 = 120
		var result = DropChanceFormulaService.CalculateDropChance(100f, true, 0.6f, 2.0f);
		Assert.Equal(120f, result, 3);
	}

	[Fact]
	public void CalculateDropChance_AllowFalseIgnoresReduction()
	{
		// allowReduction=false → skip reduction; 50 * 2.0 = 100
		var result = DropChanceFormulaService.CalculateDropChance(50f, false, 0.5f, 2.0f);
		Assert.Equal(100f, result, 6);
	}

	// --- calculateEffectiveChance ---

	[Fact]
	public void CreateEffectiveChancePlan_StaticChanceNoModifiers()
	{
		// isDynamic=false; base=50, boost=1.0, no reduction → effective=50
		var plan = DropChanceFormulaService.CreateEffectiveChancePlan(
			50f, isDynamic: false, "DISCIPLINED", "NORMAL",
			allowLevelBasedReduction: false, null, boostDropRate: 1.0f);

		Assert.Equal(50f, plan.EffectiveChance, 6);
		Assert.Equal(50f, plan.DynamicChance, 6);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateEffectiveChancePlan_DynamicMultipliesRankAndRating()
	{
		// base=100, isDynamic=true, HERO(1.8)*MASTER(1.2)=2.16; boost=1.0 → effective=216
		var plan = DropChanceFormulaService.CreateEffectiveChancePlan(
			100f, isDynamic: true, "MASTER", "HERO",
			false, null, 1.0f);

		Assert.Equal(1.2f, plan.RankModifier, 6);
		Assert.Equal(1.8f, plan.RatingModifier, 6);
		Assert.Equal(216f, plan.EffectiveChance, 4);
	}
}
