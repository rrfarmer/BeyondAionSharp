using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class NpcLevelDiffModPlanServiceTests
{
	[Theory]
	[InlineData(0, 0f)]
	[InlineData(1, 0f)]
	[InlineData(2, 0f)]
	[InlineData(-5, 0f)]
	public void GetNpcLevelDiffMod_UpTo2ReturnsZero(int diff, float expected)
	{
		Assert.Equal(expected, NpcLevelDiffModPlanService.GetNpcLevelDiffMod(diff), 6);
	}

	[Theory]
	[InlineData(3, 0.1f)]
	[InlineData(4, 0.2f)]
	[InlineData(7, 0.5f)]
	[InlineData(11, 0.9f)]
	public void GetNpcLevelDiffMod_3To11RangeFormula(int diff, float expected)
	{
		Assert.Equal(expected, NpcLevelDiffModPlanService.GetNpcLevelDiffMod(diff), 6);
	}

	[Theory]
	[InlineData(12, 1.0f)]
	[InlineData(20, 1.0f)]
	public void GetNpcLevelDiffMod_AtOrAbove12ReturnsOne(int diff, float expected)
	{
		Assert.Equal(expected, NpcLevelDiffModPlanService.GetNpcLevelDiffMod(diff), 6);
	}

	[Fact]
	public void CreatePlan_SameLevelReturnsZeroMod()
	{
		var plan = NpcLevelDiffModPlanService.CreatePlan(targetLevel: 30, attackerLevel: 30);

		Assert.Equal(0, plan.LevelDifference);
		Assert.Equal(0f, plan.Modifier, 6);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_FiveLevelHigherNpcGives30PercentMod()
	{
		// diff = 35-30 = 5; (5-2)*0.1 = 0.3
		var plan = NpcLevelDiffModPlanService.CreatePlan(targetLevel: 35, attackerLevel: 30);

		Assert.Equal(5, plan.LevelDifference);
		Assert.Equal(0.3f, plan.Modifier, 6);
	}
}
