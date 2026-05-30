using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ChatBanRemainingMinutesPlanServiceTests
{
	[Fact]
	public void CreatePlan_NullExpirationMeansNotBanned()
	{
		var plan = ChatBanRemainingMinutesPlanService.CreatePlan(banExpirationMillis: null, currentTimeMillis: 1000);

		Assert.Equal(ChatBanRemainingMinutesPlanStatus.NotBanned, plan.Status);
		Assert.Equal(0, plan.RemainingMinutes);
		Assert.False(plan.IsLive);
		Assert.False(plan.ShouldSendCanChatNow);
		Assert.Null(plan.CanChatNowMessage);
	}

	[Fact]
	public void CreatePlan_ExpiredBanSendsChatNowMessageAndReturnsZero()
	{
		// Java: if millisLeft <= 0 -> unbanPlayer -> return 0
		var plan = ChatBanRemainingMinutesPlanService.CreatePlan(
			banExpirationMillis: 1000, currentTimeMillis: 1001);

		Assert.Equal(ChatBanRemainingMinutesPlanStatus.BanExpired, plan.Status);
		Assert.Equal(0, plan.RemainingMinutes);
		Assert.True(plan.ShouldSendCanChatNow);
		Assert.NotNull(plan.CanChatNowMessage);
		Assert.Equal(1400136, plan.CanChatNowMessage!.MessageId);
	}

	[Fact]
	public void CreatePlan_ExactlyAtExpirationCountsAsExpired()
	{
		var plan = ChatBanRemainingMinutesPlanService.CreatePlan(
			banExpirationMillis: 5000, currentTimeMillis: 5000);

		Assert.Equal(ChatBanRemainingMinutesPlanStatus.BanExpired, plan.Status);
	}

	[Fact]
	public void CreatePlan_ActiveBanComputesCeilingMinutes()
	{
		// Java: Math.max(0, Math.ceil(millisLeft / 60000f))
		// millisLeft = 90000 ms → ceil(1.5) = 2 minutes
		var plan = ChatBanRemainingMinutesPlanService.CreatePlan(
			banExpirationMillis: 100000, currentTimeMillis: 10000);

		Assert.Equal(ChatBanRemainingMinutesPlanStatus.BanActive, plan.Status);
		Assert.Equal(2, plan.RemainingMinutes); // ceil(90000 / 60000) = ceil(1.5) = 2
		Assert.False(plan.ShouldSendCanChatNow);
		Assert.Null(plan.CanChatNowMessage);
	}

	[Fact]
	public void CreatePlan_ExactOneMinuteBanIsOneMinute()
	{
		// millisLeft = 60000 ms → ceil(1.0) = 1 minute
		var plan = ChatBanRemainingMinutesPlanService.CreatePlan(
			banExpirationMillis: 70000, currentTimeMillis: 10000);

		Assert.Equal(1, plan.RemainingMinutes);
	}

	[Fact]
	public void CreatePlan_PartialMinuteRoundsUp()
	{
		// millisLeft = 1 ms → ceil(1/60000) = ceil(very small positive) = 1 minute
		var plan = ChatBanRemainingMinutesPlanService.CreatePlan(
			banExpirationMillis: 10001, currentTimeMillis: 10000);

		Assert.Equal(ChatBanRemainingMinutesPlanStatus.BanActive, plan.Status);
		Assert.Equal(1, plan.RemainingMinutes);
	}

	[Fact]
	public void CreatePlan_LongActiveBanComputesCorrectMinutes()
	{
		// 60 minutes remaining = 3600000 ms → ceil(60) = 60
		var plan = ChatBanRemainingMinutesPlanService.CreatePlan(
			banExpirationMillis: 3610000, currentTimeMillis: 10000);

		Assert.Equal(60, plan.RemainingMinutes);
	}
}
