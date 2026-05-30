using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class MovementStatModifierPlanServiceTests
{
	[Fact]
	public void CreatePlan_NonPlayerReturnsOriginalValue()
	{
		var plan = MovementStatModifierPlanService.CreatePlan("PHYSICAL_ATTACK", MovementDirection.Forward, 1000f, isPlayer: false);

		Assert.Equal(1000f, plan.ModifiedValue, 6);
		Assert.False(plan.WasModified);
		Assert.False(plan.IsLive);
	}

	// --- FORWARD direction ---

	[Theory]
	[InlineData("PHYSICAL_ATTACK", 1000f, 1100f)]
	[InlineData("MAGICAL_ATTACK", 500f, 550f)]
	public void CreatePlan_ForwardBoostsAttack10Percent(string stat, float input, float expected)
	{
		var plan = MovementStatModifierPlanService.CreatePlan(stat, MovementDirection.Forward, input, true);

		Assert.Equal(expected, plan.ModifiedValue, 4);
	}

	[Theory]
	[InlineData("FIRE_RESISTANCE", 500f, 450f)]
	[InlineData("DARK_RESISTANCE", 800f, 750f)]
	public void CreatePlan_ForwardReducesElementalResistance50(string stat, float input, float expected)
	{
		var plan = MovementStatModifierPlanService.CreatePlan(stat, MovementDirection.Forward, input, true);

		Assert.Equal(expected, plan.ModifiedValue, 4);
	}

	[Fact]
	public void CreatePlan_ForwardReducesPhysicalDefense20Percent()
	{
		var plan = MovementStatModifierPlanService.CreatePlan("PHYSICAL_DEFENSE", MovementDirection.Forward, 1000f, true);

		Assert.Equal(800f, plan.ModifiedValue, 4);
	}

	// --- SIDEWAYS direction ---

	[Fact]
	public void CreatePlan_SidewaysReducesAttack20Percent()
	{
		var plan = MovementStatModifierPlanService.CreatePlan("PHYSICAL_ATTACK", MovementDirection.Sideways, 1000f, true);

		Assert.Equal(800f, plan.ModifiedValue, 4);
	}

	[Fact]
	public void CreatePlan_SidewaysAdds300Evasion()
	{
		var plan = MovementStatModifierPlanService.CreatePlan("EVASION", MovementDirection.Sideways, 500f, true);

		Assert.Equal(800f, plan.ModifiedValue, 4);
	}

	// --- BACKWARD direction ---

	[Fact]
	public void CreatePlan_BackwardReducesSpeed40Percent()
	{
		var plan = MovementStatModifierPlanService.CreatePlan("SPEED", MovementDirection.Backward, 1000f, true);

		Assert.Equal(600f, plan.ModifiedValue, 4);
	}

	[Fact]
	public void CreatePlan_BackwardAdds500ParryAndBlock()
	{
		var parry = MovementStatModifierPlanService.CreatePlan("PARRY", MovementDirection.Backward, 300f, true);
		var block = MovementStatModifierPlanService.CreatePlan("BLOCK", MovementDirection.Backward, 200f, true);

		Assert.Equal(800f, parry.ModifiedValue, 4);
		Assert.Equal(700f, block.ModifiedValue, 4);
	}
}
