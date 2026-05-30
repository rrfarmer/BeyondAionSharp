using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WarehouseExpandGuardPlanServiceTests
{
	[Fact]
	public void CreateCanExpandPlan_ZeroExpansionsAllows()
	{
		var plan = WarehouseExpandGuardPlanService.CreateCanExpandPlan(warehouseExpansions: 0);

		Assert.Equal(WarehouseExpandGuardPlanStatus.CanExpand, plan.Status);
		Assert.True(plan.CanExpand);
		Assert.False(plan.IsLive);
		Assert.Equal(1, plan.NewExpansionCount);
	}

	[Fact]
	public void CreateCanExpandPlan_AtMaxBlocksWithMessage()
	{
		// newExpansions = 11+1 = 12 > 11
		var plan = WarehouseExpandGuardPlanService.CreateCanExpandPlan(warehouseExpansions: 11);

		Assert.Equal(WarehouseExpandGuardPlanStatus.BlockedAtLimit, plan.Status);
		Assert.False(plan.CanExpand);
		Assert.NotNull(plan.DenialMessage);
	}

	[Fact]
	public void CreateCanExpandPlan_MaxExpandConstantIs11()
	{
		Assert.Equal(11, WarehouseExpandGuardPlanService.MaxExpand);
	}

	[Fact]
	public void CreateCanExpandByTicketPlan_BonusExceedsTicketLevelBlocks()
	{
		// whBonusExpands=3, completedQuests=0, effectiveBonusExpands=3 >= ticketLevel=3
		var plan = WarehouseExpandGuardPlanService.CreateCanExpandByTicketPlan(
			warehouseExpansions: 0, whBonusExpands: 3, completedWhQuests: 0, ticketLevel: 3);

		Assert.Equal(WarehouseExpandGuardPlanStatus.BlockedTicketLevelAlreadyReached, plan.Status);
		Assert.NotNull(plan.DenialMessage);
	}

	[Fact]
	public void CreateCanExpandByTicketPlan_CompletedQuestReducesEffectiveBonusExpands()
	{
		// whBonusExpands=3, completedQuests=1, effectiveBonusExpands=2 < ticketLevel=3 -> allows
		var plan = WarehouseExpandGuardPlanService.CreateCanExpandByTicketPlan(
			warehouseExpansions: 0, whBonusExpands: 3, completedWhQuests: 1, ticketLevel: 3);

		Assert.Equal(WarehouseExpandGuardPlanStatus.CanExpand, plan.Status);
	}

	[Fact]
	public void CreateCanExpandByTicketPlan_WarehouseQuestIdsAre1987And2985()
	{
		Assert.Contains(1987, WarehouseExpandGuardPlanService.WarehouseQuestIds);
		Assert.Contains(2985, WarehouseExpandGuardPlanService.WarehouseQuestIds);
	}

	[Fact]
	public void CreateCanExpandByTicketPlan_BaseGuardFailurePropagates()
	{
		// warehouseExpansions=11 → base guard fails at limit before ticket check
		var plan = WarehouseExpandGuardPlanService.CreateCanExpandByTicketPlan(
			warehouseExpansions: 11, whBonusExpands: 0, completedWhQuests: 0, ticketLevel: 5);

		Assert.Equal(WarehouseExpandGuardPlanStatus.BlockedAtLimit, plan.Status);
	}
}
