using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PvpRewardPlanServiceTests
{
	// --- PvP XP ---

	[Fact]
	public void CreateXpPlan_SameLevelSameRankBase5000()
	{
		var plan = PvpRewardPlanService.CreateXpPlan(30, 30, winnerAbyssRankId: 1, defeatedAbyssRankId: 1);

		Assert.Equal(5000, plan.FinalPoints);
		Assert.Equal(0, plan.LevelDifference);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateXpPlan_WinnerMoreThan4LevelsHigherGives10Percent()
	{
		// diff = 35-30 = 5 > 4 → round(5000 * 0.1) = 500
		var plan = PvpRewardPlanService.CreateXpPlan(35, 30, 1, 1);

		Assert.Equal(500, plan.FinalPoints);
	}

	[Fact]
	public void CreateXpPlan_DefeatedMoreThan3LevelsHigherGives130Percent()
	{
		// diff = 25-30 = -5 < -3 → round(5000 * 1.3) = 6500
		var plan = PvpRewardPlanService.CreateXpPlan(25, 30, 1, 1);

		Assert.Equal(6500, plan.FinalPoints);
	}

	[Fact]
	public void CreateXpPlan_AbyssRankPenaltyApplied()
	{
		// winner rank 5, defeated rank 3 → diff=2, 5<=7 → penalty=2*5%=10% → 5000-500=4500
		var plan = PvpRewardPlanService.CreateXpPlan(30, 30, winnerAbyssRankId: 5, defeatedAbyssRankId: 3);

		Assert.Equal(500, plan.AbyssRankPenalty);
		Assert.Equal(4500, plan.FinalPoints);
	}

	// --- PvP DP ---

	[Fact]
	public void CreateDpPlan_SameRankSameLevelBasePoints()
	{
		// (1-1)*57 + 1064 = 1064; adjusted: diff=0 → 1064 - 0 = 1064
		var plan = PvpRewardPlanService.CreateDpPlan(1, 1, defeatedLevel: 30, winnerLevel: 30);

		Assert.Equal(1064, plan.BasePoints);
		Assert.Equal(1064, plan.AdjustedPoints);
		Assert.False(plan.IsLive);
	}

	// --- adjustPvpDpGained ---

	[Fact]
	public void AdjustPvpDpGained_KillerTenLevelsHigherGivesZero()
	{
		Assert.Equal(0, PvpRewardPlanService.AdjustPvpDpGained(1000, defeatedLevel: 20, killerLevel: 30));
	}

	[Fact]
	public void AdjustPvpDpGained_KillerDefeatedTenLevelsLowerGives110Percent()
	{
		// diff = 20-30 = -10 → points *= 1.1 = 1100
		Assert.Equal(1100, PvpRewardPlanService.AdjustPvpDpGained(1000, defeatedLevel: 30, killerLevel: 20));
	}

	[Fact]
	public void AdjustPvpDpGained_FiveLevelsAboveDefeated50PercentLess()
	{
		// diff = 5 → 1000 - 1000 * 5 * 0.1 = 1000 - 500 = 500
		Assert.Equal(500, PvpRewardPlanService.AdjustPvpDpGained(1000, defeatedLevel: 25, killerLevel: 30));
	}
}
