using Aion.GameServer.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerKnownListPlayerSideEffectPlanServiceTests
{
	[Fact]
	public void PlanSee_AlwaysPlansPlayerInfoThenMotion()
	{
		var service = new PlayerKnownListPlayerSideEffectPlanService();

		var plan = service.PlanSee(new PlayerKnownListPlayerSeeSideEffectContext(ViewerPlayerObjectId, SeenPlayerObjectId));

		Assert.Equal(PlayerKnownListPlayerSideEffectTransition.See, plan.Transition);
		Assert.Equal(PlayerKnownListPlayerSideEffectStatus.Planned, plan.Status);
		Assert.False(plan.ExecutesLivePackets);
		Assert.False(plan.IsLive);
		Assert.False(plan.IsJavaControllerParity);
		Assert.Equal(
			[PlayerKnownListPlayerSideEffectKind.SmPlayerInfo, PlayerKnownListPlayerSideEffectKind.SmMotion],
			plan.Descriptors.Select(descriptor => descriptor.Kind));
		Assert.Equal("SM_PLAYER_INFO", plan.Descriptors[0].JavaPacketName);
		Assert.Equal(PlayerKnownListPlayerSideEffectCSharpSupport.Partial, plan.Descriptors[0].CSharpSupport);
		Assert.Equal("SM_MOTION", plan.Descriptors[1].JavaPacketName);
		Assert.Equal(PlayerKnownListPlayerSideEffectCSharpSupport.Available, plan.Descriptors[1].CSharpSupport);
	}

	[Fact]
	public void PlanSee_RideAndStanceAppendAfterMotionInJavaOrder()
	{
		var service = new PlayerKnownListPlayerSideEffectPlanService();

		var plan = service.PlanSee(new PlayerKnownListPlayerSeeSideEffectContext(
			ViewerPlayerObjectId,
			SeenPlayerObjectId,
			SeenIsInRideMode: true,
			SeenRideNpcId: RideNpcId,
			SeenIsUnderStance: true));

		Assert.Equal(
			[
				PlayerKnownListPlayerSideEffectKind.SmPlayerInfo,
				PlayerKnownListPlayerSideEffectKind.SmMotion,
				PlayerKnownListPlayerSideEffectKind.SmEmotionRide,
				PlayerKnownListPlayerSideEffectKind.SmPlayerStance,
			],
			plan.Descriptors.Select(descriptor => descriptor.Kind));
		Assert.Equal(RideNpcId, plan.Descriptors[2].RideNpcId);
		Assert.Equal(PlayerKnownListPlayerSideEffectCSharpSupport.Available, plan.Descriptors[2].CSharpSupport);
		Assert.Equal(1, plan.Descriptors[3].StanceState);
		Assert.Equal("SmPlayerStance", plan.Descriptors[3].CSharpPacketTypeName);
		Assert.Equal(PlayerKnownListPlayerSideEffectCSharpSupport.Available, plan.Descriptors[3].CSharpSupport);
		Assert.Contains("serializer exists", plan.Descriptors[3].Notes);
	}

	[Fact]
	public void PlanSee_AbnormalEffectsAppendAfterPlayerSpecificPackets()
	{
		var service = new PlayerKnownListPlayerSideEffectPlanService();

		var plan = service.PlanSee(new PlayerKnownListPlayerSeeSideEffectContext(
			ViewerPlayerObjectId,
			SeenPlayerObjectId,
			SeenIsInRideMode: true,
			SeenRideNpcId: RideNpcId,
			SeenIsUnderStance: true,
			SeenHasAbnormalEffects: true));

		Assert.Equal(
			[
				PlayerKnownListPlayerSideEffectKind.SmPlayerInfo,
				PlayerKnownListPlayerSideEffectKind.SmMotion,
				PlayerKnownListPlayerSideEffectKind.SmEmotionRide,
				PlayerKnownListPlayerSideEffectKind.SmPlayerStance,
				PlayerKnownListPlayerSideEffectKind.SmAbnormalEffect,
			],
			plan.Descriptors.Select(descriptor => descriptor.Kind));
		Assert.Equal("SM_ABNORMAL_EFFECT", plan.Descriptors[4].JavaPacketName);
		Assert.Equal(PlayerKnownListPlayerSideEffectCSharpSupport.Missing, plan.Descriptors[4].CSharpSupport);
	}

	[Fact]
	public void PlanSee_SelfSuppressesAggroFlagLikeJavaPlayerEqualsOwnerCheck()
	{
		var service = new PlayerKnownListPlayerSideEffectPlanService();

		var plan = service.PlanSee(new PlayerKnownListPlayerSeeSideEffectContext(
			ViewerPlayerObjectId,
			ViewerPlayerObjectId,
			ViewerAggroIconToSeen: true));

		Assert.False(plan.Descriptors[0].AggroIcon);
	}

	[Fact]
	public void PlanNotSee_WhenViewerSpawnedPlansDeleteWithAnimation()
	{
		var service = new PlayerKnownListPlayerSideEffectPlanService();

		var plan = service.PlanNotSee(new PlayerKnownListPlayerNotSeeSideEffectContext(
			ViewerPlayerObjectId,
			SeenPlayerObjectId,
			ObjectDeleteAnimation.None,
			ViewerIsSpawned: true));

		var descriptor = Assert.Single(plan.Descriptors);
		Assert.Equal(PlayerKnownListPlayerSideEffectTransition.NotSee, plan.Transition);
		Assert.Equal(PlayerKnownListPlayerSideEffectStatus.Planned, plan.Status);
		Assert.Equal(PlayerKnownListPlayerSideEffectKind.SmDelete, descriptor.Kind);
		Assert.Equal("SM_DELETE", descriptor.JavaPacketName);
		Assert.Equal("SmDelete", descriptor.CSharpPacketTypeName);
		Assert.Equal(PlayerKnownListPlayerSideEffectCSharpSupport.Available, descriptor.CSharpSupport);
		Assert.Equal(ObjectDeleteAnimation.None, descriptor.DeleteAnimation);
	}

	[Fact]
	public void PlanNotSee_WhenViewerNotSpawnedSkipsDeletePacket()
	{
		var service = new PlayerKnownListPlayerSideEffectPlanService();

		var plan = service.PlanNotSee(new PlayerKnownListPlayerNotSeeSideEffectContext(
			ViewerPlayerObjectId,
			SeenPlayerObjectId,
			ObjectDeleteAnimation.JumpIn,
			ViewerIsSpawned: false));

		Assert.Equal(PlayerKnownListPlayerSideEffectStatus.SkippedViewerNotSpawned, plan.Status);
		Assert.Empty(plan.Descriptors);
		Assert.False(plan.ExecutesLivePackets);
		Assert.Contains("viewer unspawned", plan.JavaSource);
	}

	private const int ViewerPlayerObjectId = 9001;
	private const int SeenPlayerObjectId = 9002;
	private const int RideNpcId = 730001;
}
