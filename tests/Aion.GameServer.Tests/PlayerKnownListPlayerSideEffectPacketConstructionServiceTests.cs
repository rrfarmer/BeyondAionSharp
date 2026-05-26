using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerKnownListPlayerSideEffectPacketConstructionServiceTests
{
	[Fact]
	public void Construct_SeeWithAllSuppliedFactsCreatesPacketsInJavaOrderWithoutSending()
	{
		var planner = new PlayerKnownListPlayerSideEffectPlanService();
		var service = new PlayerKnownListPlayerSideEffectPacketConstructionService();
		var sideEffectPlan = planner.PlanSee(new PlayerKnownListPlayerSeeSideEffectContext(
			ViewerPlayerObjectId,
			SeenPlayerObjectId,
			ViewerAggroIconToSeen: true,
			SeenIsInRideMode: true,
			SeenRideNpcId: RideNpcId,
			SeenIsUnderStance: true,
			SeenHasAbnormalEffects: true));

		var plan = service.Construct(new PlayerKnownListPlayerSideEffectPacketConstructionRequest(
			sideEffectPlan,
			CreateSeenPlayer(),
			ActiveMotions: [new PlayerMotion(11, 1010, true)],
			ViewerContext: new SmPlayerInfoViewerContext("ASMODIANS", ActivePlayerIsEnemyToPlayer: true),
			AbnormalEffects:
			[
				new SmAbnormalEffectEntry(
					EffectorObjectId: ViewerPlayerObjectId,
					SkillId: 3001,
					SkillLevel: 4,
					TargetSlotId: 2,
					TargetSlotOrdinal: 1,
					RemainingTimeToDisplayMillis: 45000),
			],
			AbnormalEffectMask: 0x01020304,
			AbnormalEffectSlots: 2));

		Assert.Equal(PlayerKnownListPlayerSideEffectPacketConstructionStatus.Constructed, plan.Status);
		Assert.False(plan.ExecutesLivePackets);
		Assert.False(plan.IsLive);
		Assert.False(plan.IsJavaControllerParity);
		Assert.Equal(
			[
				PlayerKnownListPlayerSideEffectKind.SmPlayerInfo,
				PlayerKnownListPlayerSideEffectKind.SmMotion,
				PlayerKnownListPlayerSideEffectKind.SmEmotionRide,
				PlayerKnownListPlayerSideEffectKind.SmPlayerStance,
				PlayerKnownListPlayerSideEffectKind.SmAbnormalEffect,
			],
			plan.Results.Select(result => result.Descriptor.Kind));
		Assert.All(plan.Results, result => Assert.Equal(
			PlayerKnownListPlayerSideEffectPacketConstructionResultStatus.Constructed,
			result.Status));
		Assert.IsType<SmPlayerInfo>(plan.Results[0].Packet);
		Assert.IsType<SmMotion>(plan.Results[1].Packet);
		Assert.IsType<SmEmotion>(plan.Results[2].Packet);
		Assert.IsType<SmPlayerStance>(plan.Results[3].Packet);
		Assert.IsType<SmAbnormalEffect>(plan.Results[4].Packet);
	}

	[Fact]
	public void Construct_SeeRideDescriptorWithoutNpcIdBlocksOnlyRidePacket()
	{
		var planner = new PlayerKnownListPlayerSideEffectPlanService();
		var service = new PlayerKnownListPlayerSideEffectPacketConstructionService();
		var sideEffectPlan = planner.PlanSee(new PlayerKnownListPlayerSeeSideEffectContext(
			ViewerPlayerObjectId,
			SeenPlayerObjectId,
			SeenIsInRideMode: true));

		var plan = service.Construct(new PlayerKnownListPlayerSideEffectPacketConstructionRequest(
			sideEffectPlan,
			CreateSeenPlayer(),
			ActiveMotions: []));

		Assert.Equal(PlayerKnownListPlayerSideEffectPacketConstructionStatus.PartiallyConstructed, plan.Status);
		Assert.Equal(PlayerKnownListPlayerSideEffectPacketConstructionResultStatus.Constructed, plan.Results[0].Status);
		Assert.Equal(PlayerKnownListPlayerSideEffectPacketConstructionResultStatus.Constructed, plan.Results[1].Status);
		Assert.Equal(
			PlayerKnownListPlayerSideEffectPacketConstructionResultStatus.BlockedMissingRideNpcId,
			plan.Results[2].Status);
		Assert.Null(plan.Results[2].Packet);
		Assert.Contains("ride NPC id", plan.Results[2].Notes);
	}

	[Fact]
	public void Construct_SeeAbnormalDescriptorWithoutEffectFactsBlocksAbnormalPacket()
	{
		var planner = new PlayerKnownListPlayerSideEffectPlanService();
		var service = new PlayerKnownListPlayerSideEffectPacketConstructionService();
		var sideEffectPlan = planner.PlanSee(new PlayerKnownListPlayerSeeSideEffectContext(
			ViewerPlayerObjectId,
			SeenPlayerObjectId,
			SeenHasAbnormalEffects: true));

		var plan = service.Construct(new PlayerKnownListPlayerSideEffectPacketConstructionRequest(
			sideEffectPlan,
			CreateSeenPlayer(),
			ActiveMotions: []));

		Assert.Equal(PlayerKnownListPlayerSideEffectPacketConstructionStatus.PartiallyConstructed, plan.Status);
		Assert.Equal(
			PlayerKnownListPlayerSideEffectPacketConstructionResultStatus.BlockedMissingAbnormalEffectFacts,
			plan.Results[2].Status);
		Assert.Null(plan.Results[2].Packet);
		Assert.Contains("EffectController", plan.Results[2].Notes);
	}

	[Fact]
	public void Construct_NotSeeCreatesDeletePacketWithoutSending()
	{
		var planner = new PlayerKnownListPlayerSideEffectPlanService();
		var service = new PlayerKnownListPlayerSideEffectPacketConstructionService();
		var sideEffectPlan = planner.PlanNotSee(new PlayerKnownListPlayerNotSeeSideEffectContext(
			ViewerPlayerObjectId,
			SeenPlayerObjectId,
			ObjectDeleteAnimation.JumpIn,
			ViewerIsSpawned: true));

		var plan = service.Construct(new PlayerKnownListPlayerSideEffectPacketConstructionRequest(
			sideEffectPlan,
			CreateSeenPlayer(),
			ActiveMotions: []));

		var result = Assert.Single(plan.Results);
		Assert.Equal(PlayerKnownListPlayerSideEffectPacketConstructionStatus.Constructed, plan.Status);
		Assert.Equal(PlayerKnownListPlayerSideEffectPacketConstructionResultStatus.Constructed, result.Status);
		Assert.IsType<SmDelete>(result.Packet);
		Assert.False(plan.ExecutesLivePackets);
	}

	[Fact]
	public void Construct_SubjectMismatchBlocksDescriptorPacketConstruction()
	{
		var planner = new PlayerKnownListPlayerSideEffectPlanService();
		var service = new PlayerKnownListPlayerSideEffectPacketConstructionService();
		var sideEffectPlan = planner.PlanSee(new PlayerKnownListPlayerSeeSideEffectContext(
			ViewerPlayerObjectId,
			SeenPlayerObjectId));

		var plan = service.Construct(new PlayerKnownListPlayerSideEffectPacketConstructionRequest(
			sideEffectPlan,
			CreatePlayer(9999),
			ActiveMotions: []));

		Assert.Equal(PlayerKnownListPlayerSideEffectPacketConstructionStatus.PartiallyConstructed, plan.Status);
		Assert.All(plan.Results, result =>
		{
			Assert.Equal(PlayerKnownListPlayerSideEffectPacketConstructionResultStatus.BlockedSubjectMismatch, result.Status);
			Assert.Null(result.Packet);
		});
	}

	private static Player CreateSeenPlayer() => CreatePlayer(SeenPlayerObjectId);

	private static Player CreatePlayer(int objectId) =>
		new()
		{
			ObjectId = objectId,
			Name = "Seen",
			Race = "ELYOS",
			Gender = "MALE",
			PlayerClass = "GLADIATOR",
			Position = new WorldPosition(210010000, 1, 2, 3, 4),
		};

	private const int ViewerPlayerObjectId = 9001;
	private const int SeenPlayerObjectId = 9002;
	private const int RideNpcId = 730001;
}
