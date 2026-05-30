using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class RoundRobinLootCounterPlanServiceTests
{
	[Fact]
	public void CreatePlan_CounterBelowGroupSizeIncrements()
	{
		var plan = RoundRobinLootCounterPlanService.CreatePlan(groupSize: 4, currentCounter: 2);

		Assert.Equal(3, plan.NewCounter);
		Assert.Equal(2, plan.WinnerIndex); // 0-based: index 2
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_CounterAtGroupSizeResetsToOne()
	{
		var plan = RoundRobinLootCounterPlanService.CreatePlan(groupSize: 4, currentCounter: 4);

		Assert.Equal(1, plan.NewCounter);
		Assert.Equal(0, plan.WinnerIndex);
	}

	[Fact]
	public void CreatePlan_CounterAboveGroupSizeAlsoResetsToOne()
	{
		// Edge case: groupSize changed since last loot
		var plan = RoundRobinLootCounterPlanService.CreatePlan(groupSize: 3, currentCounter: 5);

		Assert.Equal(1, plan.NewCounter);
	}

	[Fact]
	public void CreatePlan_FirstLootCurrentCounterZeroGivesIndex0()
	{
		// Initial state: nrRoundRobin = 0, groupSize = 3 → 3 > 0 → increment to 1 → winner[0]
		var plan = RoundRobinLootCounterPlanService.CreatePlan(groupSize: 3, currentCounter: 0);

		Assert.Equal(1, plan.NewCounter);
		Assert.Equal(0, plan.WinnerIndex);
	}

	[Fact]
	public void CreatePlan_CyclesFullGroupCorrectly()
	{
		// 3-person group: counter 0→1→2→3→1→2→3→...
		Assert.Equal(1, RoundRobinLootCounterPlanService.CreatePlan(3, 0).NewCounter);
		Assert.Equal(2, RoundRobinLootCounterPlanService.CreatePlan(3, 1).NewCounter);
		Assert.Equal(3, RoundRobinLootCounterPlanService.CreatePlan(3, 2).NewCounter);
		Assert.Equal(1, RoundRobinLootCounterPlanService.CreatePlan(3, 3).NewCounter); // wrap
	}
}
