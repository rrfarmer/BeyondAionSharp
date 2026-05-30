using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class EnchantLevelChangePlanServiceTests
{
	// --- GetAddLevel ---

	[Theory]
	[InlineData(2f,  10, 3)]  // < 5 → +3
	[InlineData(7f,  10, 2)]  // < 10 → +2
	[InlineData(50f, 10, 1)]  // >= 10 → +1
	public void GetAddLevel_ReturnsCritMultiplier(float chance, int enchant, int expected)
	{
		Assert.Equal(expected, EnchantLevelChangePlanService.GetAddLevel(chance, enchant));
	}

	[Fact]
	public void GetAddLevel_Enchant20AlwaysReturnsOne()
	{
		// Java: if (targetItem.getEnchantLevel() < 20) → no crit above 20
		Assert.Equal(1, EnchantLevelChangePlanService.GetAddLevel(1f, 20));
	}

	// --- Success outcomes ---

	[Fact]
	public void CreatePlan_SuccessIncrementsByAddLevel()
	{
		var plan = EnchantLevelChangePlanService.CreatePlan(true, 5, 15, false, 1);

		Assert.Equal(6, plan.NewEnchantLevel);
		Assert.False(plan.WasDowngraded);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_SuccessCapsAtMaxEnchant()
	{
		// currentEnchant=14 + addLevel=2 = 16 > maxEnchant=15 → cap at 15
		var plan = EnchantLevelChangePlanService.CreatePlan(true, 14, 15, false, 2);

		Assert.Equal(15, plan.NewEnchantLevel);
	}

	// --- Failure outcomes ---

	[Fact]
	public void CreatePlan_FailureAbove10ReducesToTen()
	{
		// Java: if (currentEnchant > 10 && maxEnchant > 10) → currentEnchant = 10
		var plan = EnchantLevelChangePlanService.CreatePlan(false, 13, 15, false, 1);

		Assert.Equal(10, plan.NewEnchantLevel);
		Assert.True(plan.WasDowngraded);
	}

	[Fact]
	public void CreatePlan_FailureAt5ReducesByOne()
	{
		var plan = EnchantLevelChangePlanService.CreatePlan(false, 5, 15, false, 1);

		Assert.Equal(4, plan.NewEnchantLevel);
		Assert.True(plan.WasDowngraded);
	}

	[Fact]
	public void CreatePlan_FailureAtZeroStaysZero()
	{
		var plan = EnchantLevelChangePlanService.CreatePlan(false, 0, 15, false, 1);

		Assert.Equal(0, plan.NewEnchantLevel);
		Assert.False(plan.WasDowngraded);
	}

	[Fact]
	public void CreatePlan_AmplifiedFailureResetsToMaxAndClearsFlag()
	{
		// Java: if amplified → currentEnchant = maxEnchant; setAmplified(false)
		var plan = EnchantLevelChangePlanService.CreatePlan(false, 13, 15, isAmplified: true, 1);

		Assert.Equal(15, plan.NewEnchantLevel);
		Assert.True(plan.WasAmplifiedReset);
		Assert.False(plan.WasDowngraded);
	}
}
