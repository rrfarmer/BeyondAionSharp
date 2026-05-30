using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class AutoGroupRegistrationGuardPlanServiceTests
{
	private static AutoGroupRegistrationGuardPlan Valid(int level = 30) =>
		AutoGroupRegistrationGuardPlanService.CreatePlan(
			level, minLevel: 20, maxLevel: 65,
			isPvPArenaType: false, pvpArenaAvailable: true, hasCooldown: false);

	[Fact]
	public void CreatePlan_AllGuardsPassAllowsRegistration()
	{
		var plan = Valid();

		Assert.Equal(AutoGroupRegistrationGuardPlanStatus.CanRegister, plan.Status);
		Assert.True(plan.CanRegister);
		Assert.Null(plan.DenialMessage);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_LevelTooLowBlocksWithMessage()
	{
		var plan = AutoGroupRegistrationGuardPlanService.CreatePlan(
			playerLevel: 15, minLevel: 20, maxLevel: 65,
			false, true, false);

		Assert.Equal(AutoGroupRegistrationGuardPlanStatus.BlockedLevelOutOfRange, plan.Status);
		Assert.False(plan.CanRegister);
		Assert.NotNull(plan.DenialMessage);
	}

	[Fact]
	public void CreatePlan_LevelTooHighBlocksWithMessage()
	{
		var plan = AutoGroupRegistrationGuardPlanService.CreatePlan(
			playerLevel: 70, minLevel: 20, maxLevel: 65,
			false, true, false);

		Assert.Equal(AutoGroupRegistrationGuardPlanStatus.BlockedLevelOutOfRange, plan.Status);
	}

	[Fact]
	public void CreatePlan_PvPArenaUnavailableBlocksSilently()
	{
		var plan = AutoGroupRegistrationGuardPlanService.CreatePlan(
			playerLevel: 30, minLevel: 20, maxLevel: 65,
			isPvPArenaType: true, pvpArenaAvailable: false, hasCooldown: false);

		Assert.Equal(AutoGroupRegistrationGuardPlanStatus.BlockedPvPArenaUnavailable, plan.Status);
		Assert.False(plan.CanRegister);
		Assert.Null(plan.DenialMessage); // Java sends no system message here
	}

	[Fact]
	public void CreatePlan_CooldownBlocksWithMessage()
	{
		var plan = AutoGroupRegistrationGuardPlanService.CreatePlan(
			playerLevel: 30, minLevel: 20, maxLevel: 65,
			false, true, hasCooldown: true);

		Assert.Equal(AutoGroupRegistrationGuardPlanStatus.BlockedCooldown, plan.Status);
		Assert.NotNull(plan.DenialMessage);
	}

	[Fact]
	public void CreatePlan_LevelCheckIsFirstGuard()
	{
		// Level block should fire before cooldown
		var plan = AutoGroupRegistrationGuardPlanService.CreatePlan(
			playerLevel: 5, minLevel: 20, maxLevel: 65,
			true, false, hasCooldown: true);

		Assert.Equal(AutoGroupRegistrationGuardPlanStatus.BlockedLevelOutOfRange, plan.Status);
	}
}
