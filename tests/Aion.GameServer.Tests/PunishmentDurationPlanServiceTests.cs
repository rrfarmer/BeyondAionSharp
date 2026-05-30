using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PunishmentDurationPlanServiceTests
{
	// --- Ban duration ---

	[Fact]
	public void CreateBanDurationPlan_ZeroDaysIsPermanent()
	{
		// Java: if dayCount == 0 -> return Integer.MAX_VALUE
		var plan = PunishmentDurationPlanService.CreateBanDurationPlan(dayCount: 0);

		Assert.True(plan.IsPermanent);
		Assert.Equal(0, plan.DayCount);
		Assert.Equal(int.MaxValue, plan.DurationSeconds);
		Assert.False(plan.IsLive);
		Assert.Contains("permanent", plan.JavaSource.ToLowerInvariant());
	}

	[Fact]
	public void CreateBanDurationPlan_OneDayIs86400Seconds()
	{
		var plan = PunishmentDurationPlanService.CreateBanDurationPlan(dayCount: 1);

		Assert.False(plan.IsPermanent);
		Assert.Equal(86400L, plan.DurationSeconds);
	}

	[Fact]
	public void CreateBanDurationPlan_SevenDaysIs604800Seconds()
	{
		var plan = PunishmentDurationPlanService.CreateBanDurationPlan(dayCount: 7);

		Assert.Equal(7 * 86400L, plan.DurationSeconds);
	}

	[Fact]
	public void CreateBanDurationPlan_ThirtyDaysIs2592000Seconds()
	{
		var plan = PunishmentDurationPlanService.CreateBanDurationPlan(dayCount: 30);

		Assert.Equal(30 * 86400L, plan.DurationSeconds);
	}

	// --- Prison remaining minutes ---

	[Fact]
	public void CreatePrisonRemainingMinutesPlan_ExactOneMinuteIsOne()
	{
		var plan = PunishmentDurationPlanService.CreatePrisonRemainingMinutesPlan(prisonDurationSeconds: 60);

		Assert.Equal(1, plan.RemainingMinutes);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePrisonRemainingMinutesPlan_ZeroSecondsFloorToOne()
	{
		// Java: int remainingMinutes = 0 / 60 = 0; if (remainingMinutes <= 0) remainingMinutes = 1;
		var plan = PunishmentDurationPlanService.CreatePrisonRemainingMinutesPlan(prisonDurationSeconds: 0);

		Assert.Equal(1, plan.RemainingMinutes);
	}

	[Fact]
	public void CreatePrisonRemainingMinutesPlan_PartialMinuteFloorsThenClamps()
	{
		// Java: 30 / 60 = 0; -> returns 1 (clamped)
		var plan = PunishmentDurationPlanService.CreatePrisonRemainingMinutesPlan(prisonDurationSeconds: 30);

		Assert.Equal(1, plan.RemainingMinutes);
	}

	[Fact]
	public void CreatePrisonRemainingMinutesPlan_NinetySecondsIsOne()
	{
		// Java: 90 / 60 = 1 (integer division)
		var plan = PunishmentDurationPlanService.CreatePrisonRemainingMinutesPlan(prisonDurationSeconds: 90);

		Assert.Equal(1, plan.RemainingMinutes);
	}

	[Fact]
	public void CreatePrisonRemainingMinutesPlan_SixHoursIs360Minutes()
	{
		var plan = PunishmentDurationPlanService.CreatePrisonRemainingMinutesPlan(prisonDurationSeconds: 6 * 3600);

		Assert.Equal(360, plan.RemainingMinutes);
	}
}
