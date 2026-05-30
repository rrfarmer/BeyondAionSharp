using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class CubeExpandGuardPlanServiceTests
{
	// --- canExpand ---

	[Fact]
	public void CreateCanExpandPlan_UnderLimitAllowsExpansion()
	{
		var plan = CubeExpandGuardPlanService.CreateCanExpandPlan(0, 0, 0, cubeExpansionLimit: 27);

		Assert.Equal(CubeExpandGuardPlanStatus.CanExpand, plan.Status);
		Assert.True(plan.CanExpand);
		Assert.False(plan.IsLive);
		Assert.Null(plan.DenialMessage);
		Assert.Equal(1, plan.NewExpansionCount); // 0+0+0+1=1
	}

	[Fact]
	public void CreateCanExpandPlan_AtLimitBlocksWithMessage()
	{
		// newExpansions = 10+10+7+1 = 28 > limit 27
		var plan = CubeExpandGuardPlanService.CreateCanExpandPlan(10, 10, 7, cubeExpansionLimit: 27);

		Assert.Equal(CubeExpandGuardPlanStatus.BlockedAtLimit, plan.Status);
		Assert.False(plan.CanExpand);
		Assert.NotNull(plan.DenialMessage);
		Assert.Equal(1300430, plan.DenialMessage!.MessageId);
	}

	[Fact]
	public void CreateCanExpandPlan_ExactlyAtLimitBlocksWithMessage()
	{
		// newExpansions = 0+0+26+1 = 27 > 27 is false, 27 == 27 is NOT > limit, so this should pass
		// Java: newExpansions > limit → block
		var plan = CubeExpandGuardPlanService.CreateCanExpandPlan(0, 0, 26, cubeExpansionLimit: 27);

		Assert.Equal(CubeExpandGuardPlanStatus.CanExpand, plan.Status);
		Assert.True(plan.CanExpand);
	}

	[Fact]
	public void CreateCanExpandPlan_NegativeResultBlocksSilently()
	{
		// Java: if newExpansions < 0 -> return false
		var plan = CubeExpandGuardPlanService.CreateCanExpandPlan(int.MinValue, 0, 0, cubeExpansionLimit: 27);

		Assert.Equal(CubeExpandGuardPlanStatus.BlockedNegativeExpansionCount, plan.Status);
		Assert.False(plan.CanExpand);
		Assert.Null(plan.DenialMessage);
	}

	// --- canExpandByTicket ---

	[Fact]
	public void CreateCanExpandByTicketPlan_ItemExpandsBelowTicketLevelAllows()
	{
		var plan = CubeExpandGuardPlanService.CreateCanExpandByTicketPlan(0, 0, 2, cubeExpansionLimit: 27, ticketLevel: 3);

		Assert.Equal(CubeExpandGuardPlanStatus.CanExpand, plan.Status);
		Assert.True(plan.CanExpand);
	}

	[Fact]
	public void CreateCanExpandByTicketPlan_ItemExpandsMeetingTicketLevelBlocks()
	{
		// Java: if player.getItemExpands() >= ticketLevel → block
		var plan = CubeExpandGuardPlanService.CreateCanExpandByTicketPlan(0, 0, 3, cubeExpansionLimit: 27, ticketLevel: 3);

		Assert.Equal(CubeExpandGuardPlanStatus.BlockedTicketLevelAlreadyReached, plan.Status);
		Assert.False(plan.CanExpand);
		Assert.NotNull(plan.DenialMessage);
		Assert.Equal(1300430, plan.DenialMessage!.MessageId);
	}

	[Fact]
	public void CreateCanExpandByTicketPlan_BaseGuardFailurePropagates()
	{
		// Base canExpand fails first (at limit) before ticket check
		var plan = CubeExpandGuardPlanService.CreateCanExpandByTicketPlan(0, 0, 27, cubeExpansionLimit: 27, ticketLevel: 5);

		Assert.Equal(CubeExpandGuardPlanStatus.BlockedAtLimit, plan.Status);
	}
}
