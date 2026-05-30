using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class AtreianPassportExpiryPlanServiceTests
{
	// --- isAtreianPassportDisabled ---

	[Fact]
	public void CreateIsDisabledPlan_NullExpireDateIsNotDisabled()
	{
		var plan = AtreianPassportExpiryPlanService.CreateIsDisabledPlan(
			expireDate: null, checkDateTime: DateTimeOffset.UtcNow);

		Assert.False(plan.IsDisabled);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateIsDisabledPlan_CheckDateAfterExpireIsDisabled()
	{
		var expire = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
		var check = new DateTimeOffset(2026, 5, 2, 0, 0, 0, TimeSpan.Zero);

		var plan = AtreianPassportExpiryPlanService.CreateIsDisabledPlan(expire, check);

		Assert.True(plan.IsDisabled);
	}

	[Fact]
	public void CreateIsDisabledPlan_CheckDateBeforeExpireIsNotDisabled()
	{
		var expire = new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero);
		var check = new DateTimeOffset(2026, 5, 2, 0, 0, 0, TimeSpan.Zero);

		var plan = AtreianPassportExpiryPlanService.CreateIsDisabledPlan(expire, check);

		Assert.False(plan.IsDisabled);
	}

	// --- calculatePassportExpireDate ---

	[Fact]
	public void CreateExpireDatePlan_NullLastRewardHasNoExpireDate()
	{
		var plan = AtreianPassportExpiryPlanService.CreateExpireDatePlan(lastRewardPeriodEnd: null);

		Assert.False(plan.HasExpireDate);
		Assert.Null(plan.ExpireDate);
	}

	[Fact]
	public void CreateExpireDatePlan_LastRewardPlusEndOfDayPlus14Days()
	{
		// Java: disableDateTime.toLocalDate().atTime(LocalTime.MAX).plusDays(14)
		// For lastReward = 2026-04-01 10:00 UTC → end of 2026-04-01 + 14 days = 2026-04-15 23:59:59.9...
		var lastReward = new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero);
		var plan = AtreianPassportExpiryPlanService.CreateExpireDatePlan(lastReward);

		Assert.True(plan.HasExpireDate);
		// Should be in April 2026, 15 days from the 1st (end of day 1 + 14 days)
		Assert.Equal(2026, plan.ExpireDate!.Value.Year);
		Assert.Equal(4, plan.ExpireDate.Value.Month);
		Assert.Equal(15, plan.ExpireDate.Value.Day);
	}

	[Fact]
	public void ExpiryDaysConstantIs14()
	{
		Assert.Equal(14, AtreianPassportExpiryPlanService.ExpiryDaysAfterLastReward);
	}
}
