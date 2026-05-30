using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class LootGroupDistributionPlanServiceTests
{
	// --- getAutodistributionId ---

	[Fact]
	public void CreateDistributionPlan_MythicAbove3IsBid()
	{
		var plan = LootGroupDistributionPlanService.CreateDistributionPlan(mythicItemAbove: 3);

		Assert.Equal(LootDistributionType.Bid, plan.DistributionType);
		Assert.True(plan.IsBid);
		Assert.False(plan.IsRoll);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateDistributionPlan_MythicAbove2IsRoll()
	{
		var plan = LootGroupDistributionPlanService.CreateDistributionPlan(mythicItemAbove: 2);

		Assert.Equal(LootDistributionType.Roll, plan.DistributionType);
		Assert.False(plan.IsBid);
		Assert.True(plan.IsRoll);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	public void CreateDistributionPlan_OtherValuesIsNone(int mythicAbove)
	{
		var plan = LootGroupDistributionPlanService.CreateDistributionPlan(mythicAbove);

		Assert.Equal(LootDistributionType.None, plan.DistributionType);
		Assert.False(plan.IsBid);
		Assert.False(plan.IsRoll);
	}

	[Fact]
	public void LootDistributionTypeValues_MatchJavaIds()
	{
		Assert.Equal(0, (int)LootDistributionType.None);
		Assert.Equal(2, (int)LootDistributionType.Roll);
		Assert.Equal(3, (int)LootDistributionType.Bid);
	}

	// --- getQualityRule ---

	[Fact]
	public void CreateQualityRulePlan_NonZeroThresholdIsActive()
	{
		var plan = LootGroupDistributionPlanService.CreateQualityRulePlan("UNIQUE", qualityThreshold: 1);

		Assert.True(plan.IsDistributionActive);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateQualityRulePlan_ZeroThresholdIsNotActive()
	{
		var plan = LootGroupDistributionPlanService.CreateQualityRulePlan("COMMON", qualityThreshold: 0);

		Assert.False(plan.IsDistributionActive);
	}
}
