using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class EnchantSuccessChancePlanServiceTests
{
	[Fact]
	public void CreatePlan_AmplifiedUsesBaseChanceDirectly()
	{
		var plan = EnchantSuccessChancePlanService.CreatePlan(
			isAmplified: true, baseChance: 55f, 0, 0, 5, 0f, 1);

		Assert.Equal(55f, plan.FinalChance, 6);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_LevelDiffAdjustsBy1PerLevel()
	{
		// stoneLevel > itemLevel by 5 → +5%; base=60; after=65; enchant=3 → *1.1 = 71.5; >80? no; final=71.5
		var plan = EnchantSuccessChancePlanService.CreatePlan(false, 60f, 5, 0, 3, 0f, 1);

		Assert.Equal(65f, plan.ChanceAfterAdjustments, 6);
		Assert.Equal(71.5f, plan.ChanceAfterLevelModifier, 4);
		Assert.Equal(71.5f, plan.FinalChance, 4);
	}

	[Fact]
	public void CreatePlan_QualityDiffAdjustsBy5PerTier()
	{
		// qualityDiff=2 → +10%; base=50; after=60; enchant=0 → *1.2 = 72; cap=72; final=72
		var plan = EnchantSuccessChancePlanService.CreatePlan(false, 50f, 0, 2, 0, 0f, 1);

		Assert.Equal(60f, plan.ChanceAfterAdjustments, 6);
		Assert.Equal(72f, plan.ChanceAfterLevelModifier, 4);
	}

	[Fact]
	public void CreatePlan_EnchantLevel0Boost20Percent()
	{
		// base=50, enchant=0 → 50*1.2=60; cap=60
		var plan = EnchantSuccessChancePlanService.CreatePlan(false, 50f, 0, 0, 0, 0f, 1);

		Assert.Equal(60f, plan.ChanceAfterLevelModifier, 4);
	}

	[Fact]
	public void CreatePlan_Enchant10ReduceBy10Percent()
	{
		// base=50, enchant=10 → 50*0.9=45
		var plan = EnchantSuccessChancePlanService.CreatePlan(false, 50f, 0, 0, 10, 0f, 1);

		Assert.Equal(45f, plan.ChanceAfterLevelModifier, 4);
	}

	[Fact]
	public void CreatePlan_CapAt80WithoutSupplement()
	{
		// base=90 → cap to 80
		var plan = EnchantSuccessChancePlanService.CreatePlan(false, 90f, 0, 0, 5, 0f, 1);

		Assert.Equal(80f, plan.ChanceAfterCap, 6);
		Assert.Equal(80f, plan.FinalChance, 6);
	}

	[Fact]
	public void CreatePlan_SupplementAddedAndCapAt95()
	{
		// base=70, supp=30 → 70+30=100 → cap to 95
		var plan = EnchantSuccessChancePlanService.CreatePlan(false, 70f, 0, 0, 5, 30f, 1);

		Assert.Equal(95f, plan.FinalChance, 6);
	}

	[Fact]
	public void CreatePlan_SupplementDoubledAt10Plus()
	{
		// enchant=10 → supplementCount = 1*2 = 2
		var plan = EnchantSuccessChancePlanService.CreatePlan(false, 50f, 0, 0, 10, 10f, 1);

		Assert.Equal(2, plan.SupplementUseCount);
	}

	[Fact]
	public void MaxChanceConstants_MatchJava()
	{
		Assert.Equal(80f, EnchantSuccessChancePlanService.MaxChanceWithoutSupplement);
		Assert.Equal(95f, EnchantSuccessChancePlanService.MaxChanceWithSupplement);
	}
}
