using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class NpcRewardRatingPlanServiceTests
{
	// --- DP rating multipliers ---

	[Theory]
	[InlineData("JUNK", 2)]
	[InlineData("NORMAL", 2)]
	[InlineData("ELITE", 3)]
	[InlineData("HERO", 4)]
	[InlineData("LEGENDARY", 5)]
	public void GetDpRatingMultiplier_ReturnsJavaValues(string rating, int expected)
	{
		Assert.Equal(expected, NpcRewardRatingPlanService.GetDpRatingMultiplier(rating));
	}

	// --- AP NPC rates ---

	[Theory]
	[InlineData("JUNK", 1)]
	[InlineData("NORMAL", 2)]
	[InlineData("ELITE", 4)]
	[InlineData("HERO", 35)]
	[InlineData("LEGENDARY", 2500)]
	public void GetApNpcRate_ReturnsJavaValues(string rating, int expected)
	{
		Assert.Equal(expected, NpcRewardRatingPlanService.GetApNpcRate(rating));
	}

	// --- DP reward plan ---

	[Fact]
	public void CreateDpRewardPlan_NormalMobSameLevelBaseDp()
	{
		// targetLevel=30, playerLevel=30, NORMAL → baseDP=30*2=60, xp%=100 → floor(60*100/100)=60
		var plan = NpcRewardRatingPlanService.CreateDpRewardPlan(30, 30, "NORMAL");

		Assert.Equal(60, plan.BaseDP);
		Assert.Equal(100, plan.XpPercentage);
		Assert.Equal(60, plan.BaseDpReward);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateDpRewardPlan_WeakerNpcReducedReward()
	{
		// targetLevel=24, player=30 → diff=-6 → xp%=40%; baseDP=24*3=72; reward=floor(72*40/100)=28
		var plan = NpcRewardRatingPlanService.CreateDpRewardPlan(24, 30, "ELITE");

		Assert.Equal(40, plan.XpPercentage);
		Assert.Equal(28, plan.BaseDpReward);
	}

	// --- AP reward plan ---

	[Fact]
	public void CreateApRewardPlan_NormalMobBaseAp()
	{
		// floor(15 * 2) = 30
		var plan = NpcRewardRatingPlanService.CreateApRewardPlan(30, 30, "NORMAL");

		Assert.Equal(30, plan.BaseApReward);
		Assert.False(plan.IsAboveLevel10Gap);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateApRewardPlan_PlayerMoreThan10LevelsAboveGivesOne()
	{
		var plan = NpcRewardRatingPlanService.CreateApRewardPlan(40, 25, "ELITE");

		Assert.True(plan.IsAboveLevel10Gap);
		Assert.Equal(1, plan.BaseApReward);
	}

	[Fact]
	public void CreateApRewardPlan_ExactlyTenLevelGapIsNotAbove()
	{
		// 40 - 30 = 10, NOT > 10
		var plan = NpcRewardRatingPlanService.CreateApRewardPlan(40, 30, "NORMAL");

		Assert.False(plan.IsAboveLevel10Gap);
	}
}
