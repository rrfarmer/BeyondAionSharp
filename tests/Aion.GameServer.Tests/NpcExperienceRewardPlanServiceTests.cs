using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class NpcExperienceRewardPlanServiceTests
{
	[Fact]
	public void CreatePlan_OpenWorldNpcSameLevelRewards100Percent()
	{
		// baseXP=1000, mapMulti=1.0, not group instance, xp%=100 → round(1000*1.0*1.0) = 1000
		var plan = NpcExperienceRewardPlanService.CreatePlan(
			baseXp: 1000, baseMapMultiplier: 1.0f,
			isGroupInstance: false, maxPlayers: 0, xpSoloRate: 1.0f,
			xpPercentage: 100);

		Assert.Equal(1000L, plan.RewardXp);
		Assert.Equal(1.0f, plan.EffectiveMapMultiplier, 6);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_GroupInstanceMultipliesMapMultiByPlayerCount()
	{
		// Group instance 6-player: mapMulti = 2.0 * 6 / 1.0 = 12.0; baseXP=100, xp%=100 → 1200
		var plan = NpcExperienceRewardPlanService.CreatePlan(
			baseXp: 100, baseMapMultiplier: 2.0f,
			isGroupInstance: true, maxPlayers: 6, xpSoloRate: 1.0f,
			xpPercentage: 100);

		Assert.Equal(12.0f, plan.EffectiveMapMultiplier, 6);
		Assert.Equal(1200L, plan.RewardXp);
	}

	[Fact]
	public void CreatePlan_GroupInstanceDividesBySoloRate()
	{
		// mapMulti = 1.0 * 6 / 2.0 = 3.0; baseXP=100, xp%=100 → 300
		var plan = NpcExperienceRewardPlanService.CreatePlan(
			baseXp: 100, baseMapMultiplier: 1.0f,
			isGroupInstance: true, maxPlayers: 6, xpSoloRate: 2.0f,
			xpPercentage: 100);

		Assert.Equal(3.0f, plan.EffectiveMapMultiplier, 6);
		Assert.Equal(300L, plan.RewardXp);
	}

	[Fact]
	public void CreatePlan_ReducedXpPercentage()
	{
		// baseXP=1000, mapMulti=1.0, xp%=40 (weaker mob) → round(1000*1.0*0.4) = 400
		var plan = NpcExperienceRewardPlanService.CreatePlan(
			baseXp: 1000, baseMapMultiplier: 1.0f,
			isGroupInstance: false, maxPlayers: 0, xpSoloRate: 1.0f,
			xpPercentage: 40);

		Assert.Equal(400L, plan.RewardXp);
	}

	[Fact]
	public void CreatePlan_SinglePlayerInstanceNotGroupBoosted()
	{
		// maxPlayers=1 → NOT group instance boost (Java: maxPlayers >= 2 && maxPlayers <= 6)
		var plan = NpcExperienceRewardPlanService.CreatePlan(
			baseXp: 100, baseMapMultiplier: 2.0f,
			isGroupInstance: true, maxPlayers: 1, xpSoloRate: 1.0f,
			xpPercentage: 100);

		// maxPlayers=1 doesn't satisfy >= 2, so mapMulti stays 2.0
		Assert.Equal(2.0f, plan.EffectiveMapMultiplier, 6);
	}
}
