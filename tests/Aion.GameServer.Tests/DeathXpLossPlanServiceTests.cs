using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class DeathXpLossPlanServiceTests
{
	// --- createDeathXpLossPlan ---

	[Fact]
	public void CreateDeathXpLossPlan_SplitsOneThirdUnrecoverable()
	{
		// level=20, expNeed=10000 → xpLost=100 (1%)
		// unrecoverable = (int)(100 * 0.33333333) = (int)33.3 = 33
		// recoverable = 100 - 33 = 67
		var plan = DeathXpLossPlanService.CreateDeathXpLossPlan(
			level: 20, expNeed: 10_000, currentExpShown: 5_000, currentExpRecoverable: 0);

		Assert.Equal(100L, plan.TotalXpLost);
		Assert.Equal(33, plan.UnrecoverableXp);
		Assert.Equal(67, plan.RecoverableXp);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateDeathXpLossPlan_CapsRecoverableAt25PercentExpNeed()
	{
		// If accumulated recoverable > 25% of expNeed, cap it
		// level=20, expNeed=10000 → xpLost=100, recoverable=67
		// currentExpRecoverable=2500 → allExpLost=67+2500=2567 > 10000*0.25=2500 → cap to 2500
		var plan = DeathXpLossPlanService.CreateDeathXpLossPlan(
			level: 20, expNeed: 10_000, currentExpShown: 5_000, currentExpRecoverable: 2_500);

		Assert.Equal(2_500L, plan.NewExpRecoverable);
	}

	[Fact]
	public void CreateDeathXpLossPlan_BelowCapKeepsAll()
	{
		// currentExpRecoverable=0, recoverable=67 → allExpLost=67 < 2500 → no cap
		var plan = DeathXpLossPlanService.CreateDeathXpLossPlan(
			level: 20, expNeed: 10_000, currentExpShown: 5_000, currentExpRecoverable: 0);

		Assert.Equal(67L, plan.NewExpRecoverable);
	}

	// --- createReposeMaxPlan ---

	[Fact]
	public void CreateReposeMaxPlan_ReadyPlayerUses25PercentExpNeed()
	{
		// expNeed=10000 → reposeMax = (long)(10000 * 0.25) = 2500
		var plan = DeathXpLossPlanService.CreateReposeMaxPlan(true, 10_000);

		Assert.Equal(2_500L, plan.ReposeMax);
		Assert.True(plan.IsReadyForReposeEnergy);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateReposeMaxPlan_NotReadyReturnsZero()
	{
		var plan = DeathXpLossPlanService.CreateReposeMaxPlan(false, 10_000);

		Assert.Equal(0L, plan.ReposeMax);
		Assert.False(plan.IsReadyForReposeEnergy);
	}

	[Fact]
	public void UnrecoverableFractionConstantIsOneThird()
	{
		// Java: (int)(loss * 0.33333333) ≈ 1/3
		Assert.InRange(DeathXpLossPlanService.UnrecoverableFraction, 0.333, 0.334);
	}

	[Fact]
	public void ReposeMaxFractionIs25Percent()
	{
		Assert.Equal(0.25f, DeathXpLossPlanService.ReposeMaxFraction, 6);
	}
}
