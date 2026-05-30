using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerReviveNotificationPlanServiceTests
{
	[Theory]
	[InlineData(PlayerReviveType.Skill)]
	[InlineData(PlayerReviveType.Rebirth)]
	[InlineData(PlayerReviveType.ItemSelf)]
	[InlineData(PlayerReviveType.Kisk)]
	public void CreatePlan_NonDuelReviveSendsRebirthMessage(PlayerReviveType reviveType)
	{
		var plan = PlayerReviveNotificationPlanService.CreatePlan(reviveType);

		Assert.Equal(PlayerReviveNotificationPlanStatus.WithRebirthMessage, plan.Status);
		Assert.True(plan.ShouldSendRebirthMessage);
		Assert.False(plan.IsLive);
		Assert.NotNull(plan.RebirthMessage);
		Assert.Equal(1300738, plan.RebirthMessage!.MessageId);
	}

	[Fact]
	public void CreatePlan_DuelReviveDoesNotSendRebirthMessage()
	{
		// Java: duelRevive does NOT call STR_REBIRTH_MASSAGE_ME
		var plan = PlayerReviveNotificationPlanService.CreatePlan(PlayerReviveType.Duel);

		Assert.Equal(PlayerReviveNotificationPlanStatus.NoRebirthMessage, plan.Status);
		Assert.False(plan.ShouldSendRebirthMessage);
		Assert.Null(plan.RebirthMessage);
	}
}
