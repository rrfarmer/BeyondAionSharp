using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PrivateStoreOpenGuardPlanServiceTests
{
	[Fact]
	public void CreatePlan_AllClearAllowsOpen()
	{
		var plan = Open();

		Assert.Equal(PrivateStoreOpenGuardPlanStatus.CanOpen, plan.Status);
		Assert.True(plan.CanOpen);
		Assert.False(plan.IsLive);
		Assert.Null(plan.DenialMessage);
	}

	[Fact]
	public void CreatePlan_FlyingBlocksWithJavaFlyModeMessage()
	{
		var plan = CreatePlan(isFlying: true);

		Assert.Equal(PrivateStoreOpenGuardPlanStatus.BlockedFlying, plan.Status);
		Assert.False(plan.CanOpen);
		Assert.NotNull(plan.DenialMessage);
		Assert.Equal(1300798, plan.DenialMessage!.MessageId);
		Assert.Contains("isFlying", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_InMovementBlocksWithJavaMovingMessage()
	{
		var plan = CreatePlan(isInMove: true);

		Assert.Equal(PrivateStoreOpenGuardPlanStatus.BlockedInMove, plan.Status);
		Assert.False(plan.CanOpen);
		Assert.NotNull(plan.DenialMessage);
		Assert.Equal(1300714, plan.DenialMessage!.MessageId);
	}

	[Fact]
	public void CreatePlan_InCombatModeBlocksWithJavaCombatMessage()
	{
		var plan = CreatePlan(isInCombatMode: true);

		Assert.Equal(PrivateStoreOpenGuardPlanStatus.BlockedInCombatMode, plan.Status);
		Assert.False(plan.CanOpen);
		Assert.NotNull(plan.DenialMessage);
		Assert.Equal(1300663, plan.DenialMessage!.MessageId);
	}

	[Fact]
	public void CreatePlan_TradingBlocksWithJavaCraftingMessage()
	{
		var plan = CreatePlan(isTrading: true);

		Assert.Equal(PrivateStoreOpenGuardPlanStatus.BlockedTrading, plan.Status);
		Assert.False(plan.CanOpen);
		Assert.NotNull(plan.DenialMessage);
		Assert.Equal(1400078, plan.DenialMessage!.MessageId);
	}

	[Fact]
	public void CreatePlan_RideOrRobotModeBlocksWithJavaRideMessage()
	{
		var plan = CreatePlan(isInRideOrRobotMode: true);

		Assert.Equal(PrivateStoreOpenGuardPlanStatus.BlockedRideOrRobotMode, plan.Status);
		Assert.False(plan.CanOpen);
		Assert.NotNull(plan.DenialMessage);
		Assert.Equal(1401095, plan.DenialMessage!.MessageId);
	}

	[Fact]
	public void CreatePlan_HiddenBlocksWithJavaHiddenMessage()
	{
		var plan = CreatePlan(isHidden: true);

		Assert.Equal(PrivateStoreOpenGuardPlanStatus.BlockedHidden, plan.Status);
		Assert.False(plan.CanOpen);
		Assert.NotNull(plan.DenialMessage);
		Assert.Equal(1401969, plan.DenialMessage!.MessageId);
	}

	[Fact]
	public void CreatePlan_DeadBlocksSilently()
	{
		var plan = CreatePlan(isDead: true);

		Assert.Equal(PrivateStoreOpenGuardPlanStatus.BlockedDead, plan.Status);
		Assert.False(plan.CanOpen);
		Assert.Null(plan.DenialMessage);
	}

	[Fact]
	public void CreatePlan_InChairBlocksSilently()
	{
		var plan = CreatePlan(isInChairState: true);

		Assert.Equal(PrivateStoreOpenGuardPlanStatus.BlockedInChair, plan.Status);
		Assert.False(plan.CanOpen);
		Assert.Null(plan.DenialMessage);
	}

	[Fact]
	public void CreatePlan_StoreAlreadyOpenBlocksSilently()
	{
		var plan = CreatePlan(storeAlreadyOpen: true);

		Assert.Equal(PrivateStoreOpenGuardPlanStatus.BlockedStoreAlreadyOpen, plan.Status);
		Assert.False(plan.CanOpen);
		Assert.Null(plan.DenialMessage);
	}

	[Fact]
	public void CreatePlan_FlyingBlocksBeforeAllOtherGuards()
	{
		// Java guard order: flying is first
		var plan = CreatePlan(isFlying: true, isInMove: true, isInCombatMode: true, isDead: true);

		Assert.Equal(PrivateStoreOpenGuardPlanStatus.BlockedFlying, plan.Status);
	}

	[Fact]
	public void CreatePlan_SilentDeadGuardComesAfterMessagesGuards()
	{
		// Java guard order: dead comes after all message guards
		var plan = CreatePlan(isDead: true, isInChairState: true, storeAlreadyOpen: true);

		Assert.Equal(PrivateStoreOpenGuardPlanStatus.BlockedDead, plan.Status);
	}

	private static PrivateStoreOpenGuardPlan Open()
	{
		return CreatePlan();
	}

	private static PrivateStoreOpenGuardPlan CreatePlan(
		bool isFlying = false,
		bool isInMove = false,
		bool isInCombatMode = false,
		bool isTrading = false,
		bool isInRideOrRobotMode = false,
		bool isHidden = false,
		bool isDead = false,
		bool isInChairState = false,
		bool storeAlreadyOpen = false)
	{
		return PrivateStoreOpenGuardPlanService.CreatePlan(
			isFlying, isInMove, isInCombatMode, isTrading,
			isInRideOrRobotMode, isHidden, isDead, isInChairState, storeAlreadyOpen);
	}
}
