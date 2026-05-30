using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class MagicalResistRatePlanServiceTests
{
	[Fact]
	public void CreatePlan_AlwaysResistReturns1000()
	{
		var plan = MagicalResistRatePlanService.CreatePlan(30, 30, 200, 100, 0, isAlwaysResist: true, isPvP: false);

		Assert.Equal(1000, plan.FinalResistRate);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_BasicResistRate()
	{
		// mResist=300, mAcc=200, accMod=0, levelDiff=0, PvE → min(900, 100) = 100
		var plan = MagicalResistRatePlanService.CreatePlan(30, 30, 300, 200, 0, false, isPvP: false);

		Assert.Equal(100, plan.FinalResistRate);
		Assert.Equal(0, plan.LevelBonusAdded);
	}

	[Fact]
	public void CreatePlan_LevelDiffAbove4AddsBonus()
	{
		// defender level 40, attacker level 30 → levelDiff=10; bonus=(10-4)*100=600
		// base = 300-200 = 100; total = 700; cap PvE=900 → 700
		var plan = MagicalResistRatePlanService.CreatePlan(30, 40, 300, 200, 0, false, isPvP: false);

		Assert.Equal(600, plan.LevelBonusAdded);
		Assert.Equal(700, plan.FinalResistRate);
	}

	[Fact]
	public void CreatePlan_PvPCapsAt500()
	{
		// Large resist difference → uncapped = 800; PvP cap = 500
		var plan = MagicalResistRatePlanService.CreatePlan(20, 40, 1000, 200, 0, false, isPvP: true);

		Assert.Equal(500, plan.FinalResistRate);
	}

	[Fact]
	public void CreatePlan_LevelBonusOnlyAppliesWhenMResistPositive()
	{
		// mResist=0, levelDiff=10 → no bonus
		var plan = MagicalResistRatePlanService.CreatePlan(20, 30, 0, 200, 0, false, isPvP: false);

		Assert.Equal(0, plan.LevelBonusAdded);
	}
}
