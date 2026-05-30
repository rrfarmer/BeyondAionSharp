using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class InstanceServiceFormulaServiceTests
{
	// --- Cooldown rate ---

	[Fact]
	public void CreateCooldownRatePlan_PermissionAndNotExcludedUsesConfigRate()
	{
		var plan = InstanceServiceFormulaService.CreateCooldownRatePlan(
			hasInstanceCooldownPermission: true, mapIsExcluded: false, instanceCooldownRate: 2);

		Assert.Equal(2, plan.CooldownRate);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateCooldownRatePlan_NoPermissionUsesOne()
	{
		var plan = InstanceServiceFormulaService.CreateCooldownRatePlan(
			hasInstanceCooldownPermission: false, mapIsExcluded: false, instanceCooldownRate: 2);

		Assert.Equal(1, plan.CooldownRate);
	}

	[Fact]
	public void CreateCooldownRatePlan_ExcludedMapUsesOne()
	{
		var plan = InstanceServiceFormulaService.CreateCooldownRatePlan(
			hasInstanceCooldownPermission: true, mapIsExcluded: true, instanceCooldownRate: 2);

		Assert.Equal(1, plan.CooldownRate);
	}

	// --- Destroy delay ---

	[Fact]
	public void CreateDestroyDelayPlan_SoloInstanceUsesSoloDelay()
	{
		var plan = InstanceServiceFormulaService.CreateDestroyDelayPlan(
			maxPlayers: 1, soloDestroyDelaySeconds: 300, normalDestroyDelaySeconds: 1800);

		Assert.True(plan.IsSolo);
		Assert.Equal(300, plan.DestroyDelaySeconds);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateDestroyDelayPlan_GroupInstanceUsesNormalDelay()
	{
		var plan = InstanceServiceFormulaService.CreateDestroyDelayPlan(
			maxPlayers: 6, soloDestroyDelaySeconds: 300, normalDestroyDelaySeconds: 1800);

		Assert.False(plan.IsSolo);
		Assert.Equal(1800, plan.DestroyDelaySeconds);
	}

	[Fact]
	public void CreateDestroyDelayPlan_MaxPlayersOfTwoIsNotSolo()
	{
		var plan = InstanceServiceFormulaService.CreateDestroyDelayPlan(
			maxPlayers: 2, soloDestroyDelaySeconds: 300, normalDestroyDelaySeconds: 1800);

		Assert.False(plan.IsSolo);
		Assert.Equal(1800, plan.DestroyDelaySeconds);
	}
}
