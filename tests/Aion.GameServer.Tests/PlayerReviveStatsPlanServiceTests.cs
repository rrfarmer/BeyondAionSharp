using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerReviveStatsPlanServiceTests
{
	[Fact]
	public void CreatePlan_DuelReviveUses30PercentHpMp()
	{
		// Java: duelRevive -> revive(player, 30, 30, false, 0)
		var plan = PlayerReviveStatsPlanService.CreatePlan(
			hpPercent: PlayerReviveStatsPlanService.DuelReviveHpPercent,
			mpPercent: PlayerReviveStatsPlanService.DuelReviveMpPercent,
			maxHp: 10000, maxMp: 5000,
			currentDp: 0, isNoResurrectPenalty: false,
			setSoulSickness: false);

		Assert.Equal(PlayerReviveStatsPlanStatus.PlanCreated, plan.Status);
		Assert.Equal(30, plan.HpPercent);
		Assert.Equal(30, plan.MpPercent);
		Assert.Equal(3000, plan.RestoredHp); // 10000 * 30 / 100
		Assert.Equal(1500, plan.RestoredMp); // 5000 * 30 / 100
		Assert.False(plan.ShouldClearDp);
		Assert.False(plan.ShouldSetSoulSickness);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_SkillReviveUses35PercentWithSoulSickness()
	{
		// Java: skillRevive -> revive(player, 35, 35, true, resurrectionSkill)
		var plan = PlayerReviveStatsPlanService.CreatePlan(
			hpPercent: PlayerReviveStatsPlanService.SkillReviveHpPercent,
			mpPercent: PlayerReviveStatsPlanService.SkillReviveMpPercent,
			maxHp: 10000, maxMp: 5000,
			currentDp: 500, isNoResurrectPenalty: false,
			setSoulSickness: true, resurrectionSkillId: 12345);

		Assert.Equal(35, plan.HpPercent);
		Assert.Equal(3500, plan.RestoredHp);
		Assert.True(plan.ShouldSetSoulSickness);
		Assert.Equal(12345, plan.ResurrectionSkillId);
		Assert.True(plan.ShouldClearDp); // dp=500 > 0 && !noResurrectPenalty
	}

	[Fact]
	public void CreatePlan_NoResurrectPenaltyOverridesToFullRestore()
	{
		// Java: isNoResurrectPenalty -> HP=100%, MP=100%, no DP clear, no soul sickness
		var plan = PlayerReviveStatsPlanService.CreatePlan(
			hpPercent: 35, mpPercent: 35,
			maxHp: 10000, maxMp: 5000,
			currentDp: 1000, isNoResurrectPenalty: true,
			setSoulSickness: true);

		Assert.Equal(100, plan.HpPercent);
		Assert.Equal(100, plan.MpPercent);
		Assert.Equal(10000, plan.RestoredHp);
		Assert.Equal(5000, plan.RestoredMp);
		Assert.True(plan.IsNoResurrectPenalty);
		Assert.False(plan.ShouldClearDp);    // overridden by isNoResurrectPenalty
		Assert.False(plan.ShouldSetSoulSickness); // overridden by isNoResurrectPenalty
	}

	[Fact]
	public void CreatePlan_ZeroDpDoesNotClearDp()
	{
		var plan = PlayerReviveStatsPlanService.CreatePlan(
			30, 30, 10000, 5000, currentDp: 0, false, false);

		Assert.False(plan.ShouldClearDp);
	}

	[Fact]
	public void Constants_MatchJavaReviveService()
	{
		Assert.Equal(30, PlayerReviveStatsPlanService.DuelReviveHpPercent);
		Assert.Equal(30, PlayerReviveStatsPlanService.DuelReviveMpPercent);
		Assert.Equal(35, PlayerReviveStatsPlanService.SkillReviveHpPercent);
		Assert.Equal(35, PlayerReviveStatsPlanService.SkillReviveMpPercent);
		Assert.Equal(100, PlayerReviveStatsPlanService.NoResurrectPenaltyPercent);
		Assert.Equal(5, PlayerReviveStatsPlanService.FallbackRebirthPercent);
	}
}
