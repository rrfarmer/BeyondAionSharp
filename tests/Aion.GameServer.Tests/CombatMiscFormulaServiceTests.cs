using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class CombatMiscFormulaServiceTests
{
	// --- calculatePvPApLost ---

	[Fact]
	public void CreatePvpApLostPlan_SameLevelNoAdjustment()
	{
		var plan = CombatMiscFormulaService.CreatePvpApLostPlan(30, 30, 1000);

		Assert.Equal(1000, plan.AdjustedPointsLost);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePvpApLostPlan_WinnerFiveLevelsHigherGives10Percent()
	{
		// diff=5 → round(1000 * 0.1) = 100
		var plan = CombatMiscFormulaService.CreatePvpApLostPlan(35, 30, 1000);

		Assert.Equal(100, plan.AdjustedPointsLost);
	}

	[Fact]
	public void CreatePvpApLostPlan_Diff4Gives65Percent()
	{
		var plan = CombatMiscFormulaService.CreatePvpApLostPlan(34, 30, 1000);

		Assert.Equal(650, plan.AdjustedPointsLost);
	}

	// --- calculateFallDamage ---

	[Fact]
	public void CreateFallDamagePlan_ShortFallNoDamage()
	{
		var plan = CombatMiscFormulaService.CreateFallDamagePlan(3f, 5f, 50f, 10f, 5000, 10000);

		Assert.Equal(0, plan.FallDamage);
		Assert.False(plan.IsLethalFall);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateFallDamagePlan_MediumFallCausesDamage()
	{
		// distance=10, min=5, max=50, 10% of 10000=1000 per unit → 10*1000/10=100 damage ... wait
		// dmgPerMeter = 10000 * 10f / 100f = 1000; fallDamage = (int)(10 * 1000) = 10000
		var plan = CombatMiscFormulaService.CreateFallDamagePlan(10f, 5f, 50f, 10f, 5000, 10000);

		Assert.Equal(10000, plan.FallDamage);
		Assert.False(plan.IsLethalFall);
	}

	[Fact]
	public void CreateFallDamagePlan_MaximumFallIsLethal()
	{
		var plan = CombatMiscFormulaService.CreateFallDamagePlan(100f, 5f, 50f, 10f, 3500, 10000);

		Assert.True(plan.IsLethalFall);
		Assert.Equal(3500, plan.FallDamage); // = currentHp
	}
}
