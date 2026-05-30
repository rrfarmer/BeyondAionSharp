using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class NpcDecayDelayPlanServiceTests
{
	// --- decay delay ---

	[Fact]
	public void CreateDecayDelayPlan_NoDropUsesImmediateDecay()
	{
		var plan = NpcDecayDelayPlanService.CreateDecayDelayPlan(hasDrop: false);

		Assert.Equal(NpcDecayDelayPlanStatus.ImmediateDecay, plan.Status);
		Assert.Equal(2000, plan.DelayMilliseconds);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateDecayDelayPlan_WithDropUsesLongDecay()
	{
		var plan = NpcDecayDelayPlanService.CreateDecayDelayPlan(hasDrop: true);

		Assert.Equal(NpcDecayDelayPlanStatus.WithDropDecay, plan.Status);
		Assert.Equal(300000, plan.DelayMilliseconds);
	}

	[Fact]
	public void CreateDecayDelayPlanWithDelay_ZeroDelayBecomesImmediateDecay()
	{
		// Java: if delay == 0 -> delay = IMMEDIATE_DECAY (for death animation)
		var plan = NpcDecayDelayPlanService.CreateDecayDelayPlanWithDelay(requestedDelayMilliseconds: 0);

		Assert.Equal(NpcDecayDelayPlanStatus.ImmediateDecay, plan.Status);
		Assert.Equal(2000, plan.DelayMilliseconds);
		Assert.Contains("death animation", plan.JavaSource);
	}

	[Fact]
	public void CreateDecayDelayPlanWithDelay_NonZeroDelayIsCustom()
	{
		var plan = NpcDecayDelayPlanService.CreateDecayDelayPlanWithDelay(requestedDelayMilliseconds: 5000);

		Assert.Equal(NpcDecayDelayPlanStatus.CustomDelay, plan.Status);
		Assert.Equal(5000, plan.DelayMilliseconds);
	}

	[Fact]
	public void Constants_MatchJavaValues()
	{
		Assert.Equal(2000L, NpcDecayDelayPlanService.ImmediateDecayMilliseconds);
		Assert.Equal(300000L, NpcDecayDelayPlanService.WithDropDecayMilliseconds);
	}

	// --- respawn ---

	[Fact]
	public void CreateRespawnPlan_NoSpawnTemplateReturnsNoTemplate()
	{
		var plan = NpcDecayDelayPlanService.CreateRespawnPlan(hasSpawnTemplate: false, isNoRespawn: false, respawnTimeSeconds: 60);

		Assert.Equal(NpcRespawnPlanStatus.NoRespawnTemplate, plan.Status);
		Assert.Equal(0, plan.ScheduledDelayMilliseconds);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateRespawnPlan_NoRespawnFlagPreventsRespawn()
	{
		var plan = NpcDecayDelayPlanService.CreateRespawnPlan(hasSpawnTemplate: true, isNoRespawn: true, respawnTimeSeconds: 60);

		Assert.Equal(NpcRespawnPlanStatus.NoRespawnFlagged, plan.Status);
		Assert.Equal(0, plan.ScheduledDelayMilliseconds);
	}

	[Fact]
	public void CreateRespawnPlan_ValidTemplateSchedulesRespawn()
	{
		var plan = NpcDecayDelayPlanService.CreateRespawnPlan(hasSpawnTemplate: true, isNoRespawn: false, respawnTimeSeconds: 30);

		Assert.Equal(NpcRespawnPlanStatus.ShouldRespawn, plan.Status);
		Assert.Equal(30, plan.RespawnTimeSeconds);
		Assert.Equal(30000L, plan.ScheduledDelayMilliseconds); // 30s * 1000ms
	}
}
