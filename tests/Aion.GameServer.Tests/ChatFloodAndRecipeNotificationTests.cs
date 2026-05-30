using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ChatFloodAndRecipeNotificationTests
{
	// --- Chat flood detection ---

	[Fact]
	public void ChatFloodDetection_BelowLimitIsNotFlooding()
	{
		var plan = ChatFloodDetectionPlanService.CreatePlan(currentFloodMsgCount: 5, floodMsgLimit: 10);

		Assert.Equal(ChatFloodDetectionPlanStatus.NotFlooding, plan.Status);
		Assert.False(plan.IsFlooding);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void ChatFloodDetection_AboveLimitIsFlooding()
	{
		var plan = ChatFloodDetectionPlanService.CreatePlan(currentFloodMsgCount: 11, floodMsgLimit: 10);

		Assert.Equal(ChatFloodDetectionPlanStatus.IsFlooding, plan.Status);
		Assert.True(plan.IsFlooding);
	}

	[Fact]
	public void ChatFloodDetection_ExactlyAtLimitIsNotFlooding()
	{
		// Java: floodMsgCount() > FLOOD_MSG (strict greater-than)
		var plan = ChatFloodDetectionPlanService.CreatePlan(currentFloodMsgCount: 10, floodMsgLimit: 10);

		Assert.Equal(ChatFloodDetectionPlanStatus.NotFlooding, plan.Status);
		Assert.False(plan.IsFlooding);
	}

	// --- Recipe add notification ---

	[Fact]
	public void RecipeAddNotification_ProducesLearnMessage()
	{
		var plan = RecipeAddNotificationPlanService.CreatePlan(recipeId: 12345, playerName: "Daeva");

		Assert.Equal(RecipeAddNotificationPlanStatus.PlanCreated, plan.Status);
		Assert.True(plan.ShouldSendToPlayer);
		Assert.NotNull(plan.LearnNotification);
		Assert.Equal(12345, plan.RecipeId);
		Assert.Equal("Daeva", plan.PlayerName);
		Assert.False(plan.IsLive);
	}
}
