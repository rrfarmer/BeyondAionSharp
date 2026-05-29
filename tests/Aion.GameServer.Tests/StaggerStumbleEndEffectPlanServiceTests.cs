using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class StaggerStumbleEndEffectPlanServiceTests
{
	[Theory]
	[InlineData(ForcedMoveEffectKind.Stagger, "STAGGER", "StaggerEffect.endEffect")]
	[InlineData(ForcedMoveEffectKind.Stumble, "STUMBLE", "StumbleEffect.endEffect")]
	public void CreatePlan_ForStaggerOrStumbleUnsetsMatchingAbnormalLikeJava(
		ForcedMoveEffectKind effectKind,
		string abnormalStateName,
		string javaSource)
	{
		var plan = StaggerStumbleEndEffectPlanService.CreatePlan(
			new StaggerStumbleEndEffectPlanInput(effectKind, EffectedObjectId: 8002));

		Assert.Equal(StaggerStumbleEndEffectPlanStatus.Planned, plan.Status);
		Assert.False(plan.IsLive);
		Assert.Equal(abnormalStateName, plan.AbnormalStateName);
		Assert.True(plan.ShouldUnsetAbnormal);
		Assert.Contains(javaSource, plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_BlocksInvalidEffectedBeforeUnsetIntent()
	{
		var plan = StaggerStumbleEndEffectPlanService.CreatePlan(
			new StaggerStumbleEndEffectPlanInput(ForcedMoveEffectKind.Stagger, EffectedObjectId: 0));

		Assert.Equal(StaggerStumbleEndEffectPlanStatus.BlockedInvalidEffected, plan.Status);
		Assert.Equal("STAGGER", plan.AbnormalStateName);
		Assert.False(plan.ShouldUnsetAbnormal);
		Assert.Contains("live effected Creature", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_RejectsUnsupportedForcedMoveEffectKinds()
	{
		var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
			StaggerStumbleEndEffectPlanService.CreatePlan(
				new StaggerStumbleEndEffectPlanInput(ForcedMoveEffectKind.Pulled, EffectedObjectId: 8002)));

		Assert.Contains("Only stagger/stumble", exception.Message);
	}
}
