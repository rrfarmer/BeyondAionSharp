using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class NpcBaseExpPlanServiceTests
{
	[Theory]
	[InlineData("JUNK", 2.0f)]
	[InlineData("NORMAL", 2.2f)]
	[InlineData("ELITE", 4.0f)]
	[InlineData("HERO", 5.4f)]
	[InlineData("LEGENDARY", 6.4f)]
	public void GetRatingMultiplier_ReturnsJavaValues(string rating, float expected)
	{
		Assert.Equal(expected, NpcBaseExpPlanService.GetRatingMultiplier(rating), 6);
	}

	[Fact]
	public void CreatePlan_ZeroMaxHpReturnsZeroExp()
	{
		var plan = NpcBaseExpPlanService.CreatePlan(maxHp: 0, "NORMAL", rankOrdinal: 0);

		Assert.Equal(0, plan.BaseExp);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_NormalRankZeroMobBaseExp()
	{
		// maxHp=1000, rating=NORMAL(2.2f), rank=0 → 1000 * 2.2 = 2200
		var plan = NpcBaseExpPlanService.CreatePlan(maxHp: 1000, "NORMAL", rankOrdinal: 0);

		Assert.Equal(2200, plan.BaseExp);
		Assert.Equal(2.2f, plan.TotalMultiplier, 6);
	}

	[Fact]
	public void CreatePlan_RankBonusAddsToMultiplier()
	{
		// maxHp=1000, ELITE(4.0f) + rank1(0.2f) = 4.2 → 4200
		var plan = NpcBaseExpPlanService.CreatePlan(maxHp: 1000, "ELITE", rankOrdinal: 1);

		Assert.Equal(4200, plan.BaseExp);
		Assert.Equal(0.2f, plan.RankBonus, 6);
	}

	[Fact]
	public void CreatePlan_IsCaseInsensitive()
	{
		var plan = NpcBaseExpPlanService.CreatePlan(1000, "normal", 0);
		Assert.Equal(2200, plan.BaseExp);
	}
}
