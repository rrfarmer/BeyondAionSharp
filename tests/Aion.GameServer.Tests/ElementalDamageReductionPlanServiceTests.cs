using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ElementalDamageReductionPlanServiceTests
{
	// --- getElementalDefenseDenominator ---

	[Fact]
	public void CreateDenominatorPlan_NpcTargetAlwaysBase1300()
	{
		var plan = ElementalDamageReductionPlanService.CreateDenominatorPlan(false, 60, 60);

		Assert.Equal(1300, plan.Denominator);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateDenominatorPlan_PlayerTargetBelow50IsBase1300()
	{
		// min(40, 45) = 40; max(0, 40-50) = 0 → 1300
		var plan = ElementalDamageReductionPlanService.CreateDenominatorPlan(true, 40, 45);

		Assert.Equal(1300, plan.Denominator);
	}

	[Fact]
	public void CreateDenominatorPlan_PlayerTargetUsesMinLevel()
	{
		// min(60, 65) = 60; max(0, 60-50)*10 = 100 → 1400
		var plan = ElementalDamageReductionPlanService.CreateDenominatorPlan(true, 60, 65);

		Assert.Equal(1400, plan.Denominator);
	}

	[Fact]
	public void CreateDenominatorPlan_EffectorLevelIsLimitingFactor()
	{
		// min(55, 65) = 55; max(0, 55-50)*10 = 50 → 1350
		var plan = ElementalDamageReductionPlanService.CreateDenominatorPlan(true, 55, 65);

		Assert.Equal(1350, plan.Denominator);
	}

	// --- reduceDamageByElementalDefense ---

	[Fact]
	public void CreateReductionPlan_ZeroDefenseNoDamageReduction()
	{
		var plan = ElementalDamageReductionPlanService.CreateReductionPlan(1000f, 0, 1300);

		Assert.Equal(1000f, plan.ReducedDamage, 3);
		Assert.Equal(1.0f, plan.ReductionFactor, 6);
	}

	[Fact]
	public void CreateReductionPlan_HalfDenominatorDefenseReduces50Percent()
	{
		// adjustedDefense=650, denominator=1300 → factor = 1 - 650/1300 = 0.5
		var plan = ElementalDamageReductionPlanService.CreateReductionPlan(1000f, 650, 1300);

		Assert.Equal(0.5f, plan.ReductionFactor, 6);
		Assert.Equal(500f, plan.ReducedDamage, 3);
	}

	[Fact]
	public void CreateReductionPlan_MaxDefenseNearZeroDamage()
	{
		// adjustedDefense=1300, denominator=1300 → factor = 0 → damage=0
		var plan = ElementalDamageReductionPlanService.CreateReductionPlan(1000f, 1300, 1300);

		Assert.Equal(0f, plan.ReducedDamage, 3);
	}

	[Fact]
	public void ElementalDefenseBaseConstantIs1300()
	{
		Assert.Equal(1300, ElementalDamageReductionPlanService.ElementalDefenseBase);
	}
}
