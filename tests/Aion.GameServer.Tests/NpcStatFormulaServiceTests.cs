using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class NpcStatFormulaServiceTests
{
	// --- calculateHate ---

	[Fact]
	public void CreateHatePlan_BaseBoost100GivesSameValue()
	{
		// (long)1000 * (1000 + 100) / 1000 = 1100
		var plan = NpcStatFormulaService.CreateHatePlan(value: 1000, boostHate: 100);

		Assert.Equal(1100, plan.Result);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateHatePlan_ZeroBoostGivesSameValue()
	{
		// (long)500 * (1000 + 0) / 1000 = 500
		var plan = NpcStatFormulaService.CreateHatePlan(value: 500, boostHate: 0);

		Assert.Equal(500, plan.Result);
	}

	[Fact]
	public void CreateHatePlan_LargeValueUsesLongArithmetic()
	{
		// Java: (long) value * ... avoids int overflow
		var plan = NpcStatFormulaService.CreateHatePlan(value: 2_000_000, boostHate: 200);

		Assert.Equal(2_400_000, plan.Result);
	}

	// --- calculatePvpApGained ---

	[Fact]
	public void CreatePvpApPlan_SameLevelSameRankUsesBasePoints()
	{
		var plan = NpcStatFormulaService.CreatePvpApPlan(30, 30, 1, 1, defeatedPointsGained: 1000);

		Assert.Equal(1000, plan.FinalPoints);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePvpApPlan_WinnerFiveLevelsHigherGives10Percent()
	{
		// diff=5 > 4 → round(1000 * 0.1) = 100
		var plan = NpcStatFormulaService.CreatePvpApPlan(35, 30, 1, 1, 1000);

		Assert.Equal(100, plan.FinalPoints);
	}
}
