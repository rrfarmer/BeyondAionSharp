using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PrivateStoreSellNotificationPlanServiceTests
{
	[Fact]
	public void CreatePlan_CountOneUsesSingularMessage()
	{
		var plan = PrivateStoreSellNotificationPlanService.CreatePlan(count: 1, itemName: "Manastone: Magic Boost +7");

		Assert.Equal(PrivateStoreSellNotificationPlanStatus.PlanCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldSendToSeller);
		Assert.NotNull(plan.NotificationMessage);
		Assert.Equal(1400134, plan.NotificationMessage!.MessageId);
		Assert.Contains("SELL_ITEM", plan.JavaSource);
		Assert.DoesNotContain("MULTI", plan.JavaSource);
	}

	[Theory]
	[InlineData(2)]
	[InlineData(10)]
	[InlineData(99)]
	public void CreatePlan_CountAboveOneUsesMultiMessage(long count)
	{
		var plan = PrivateStoreSellNotificationPlanService.CreatePlan(count, itemName: "Manastone: Magic Boost +7");

		Assert.Equal(PrivateStoreSellNotificationPlanStatus.PlanCreated, plan.Status);
		Assert.True(plan.ShouldSendToSeller);
		Assert.NotNull(plan.NotificationMessage);
		Assert.Equal(1400135, plan.NotificationMessage!.MessageId);
		Assert.Contains("MULTI", plan.JavaSource);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void CreatePlan_EmptyItemNameBlocks(string? itemName)
	{
		var plan = PrivateStoreSellNotificationPlanService.CreatePlan(count: 1, itemName: itemName);

		Assert.Equal(PrivateStoreSellNotificationPlanStatus.BlockedEmptyItemName, plan.Status);
		Assert.False(plan.ShouldSendToSeller);
		Assert.Null(plan.NotificationMessage);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void CreatePlan_ZeroOrNegativeCountBlocks(long count)
	{
		var plan = PrivateStoreSellNotificationPlanService.CreatePlan(count, itemName: "Manastone");

		Assert.Equal(PrivateStoreSellNotificationPlanStatus.BlockedZeroOrNegativeCount, plan.Status);
		Assert.False(plan.ShouldSendToSeller);
		Assert.Null(plan.NotificationMessage);
	}
}
